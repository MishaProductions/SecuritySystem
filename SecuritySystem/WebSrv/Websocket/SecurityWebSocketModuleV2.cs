using EmbedIO.WebSockets;
using MHSApi.API;
using MHSApi.WebSocket;
using NAudio.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecuritySystem.DeviceSubsys;
using SecuritySystem.Utils;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SecuritySystem.WebSrv.Websocket
{
    public class SecurityWebSocketModuleV2 : WebSocketModule
    {
        // Message types:
        //1: welcome (from server)
        //2: hello (from client)
        //3: error (from server)
        //4: auth success (from server)
        //5: get zones (from server)
        //6: Countdown (from server)
        //7: System is disarmed (from server)


        //<Id, Authenticated>
        readonly Dictionary<string, User?> AuthenticatedSessions = new();

        public SecurityWebSocketModuleV2(string urlPath)
         : base(urlPath, true)
        {
            SystemManager.OnZoneUpdate += SystemManager_OnZoneUpdate;
            SystemManager.OnSystemDisarm += SystemManager_OnSystemDisarm;
            SystemManager.OnAlarm += SystemManager_AnnounceAlarm;
            SystemManager.OnSysTimerEvent += SystemManager_SysTimerEvent;

            MusicPlayer.OnAnncStop += MusicPlayer_OnAnncStop;
            MusicPlayer.OnMusicStop += MusicPlayer_OnMusicStop;

            MusicPlayer.OnMusicVolumeChanged += MusicPlayer_OnMusicVolumeChanged;
            MusicPlayer.OnAnncVolumeChanged += MusicPlayer_OnAnncVolumeChanged;

            DeviceModel.FirmwareUpdateEvent += DeviceModel_OnFirmwareUpdateProgress;
        }

        private async void DeviceModel_OnFirmwareUpdateProgress(string devName, string desc, int percent)
        {
            Console.WriteLine("send " + devName + ", " + desc + ", " + percent);
            await SendToAll(new FwUpdateMsg(devName, desc, percent));
        }

        private async void MusicPlayer_OnAnncVolumeChanged(object? sender, EventArgs e)
        {
            await SendToAll(new AnncPlayerVolumeChange(MusicPlayer.Anncvol));
        }

        private async void MusicPlayer_OnMusicVolumeChanged(object? sender, EventArgs e)
        {
            await SendToAll(new MusicPlayerVolumeChange(MusicPlayer.MusicVol));
        }

        private void MusicPlayer_OnMusicStop(object? sender, EventArgs e)
        {

        }

        private void MusicPlayer_OnAnncStop(object? sender, EventArgs e)
        {

        }

        private string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        private async Task<bool> IsAuthed(IWebSocketContext context)
        {
            if (AuthenticatedSessions.ContainsKey(context.Session.Id))
            {
                if (AuthenticatedSessions[context.Session.Id] != null)
                {
                    return true;

                }
                else
                {
                    await SendAsync(context, Serialize(new AuthenticationFail("You must authenticate to use the websocket!")));
                    return false;
                }
            }
            else
            {
                await SendAsync(context, Serialize(new AuthenticationFail("You must authenticate to use the websocket. You must send the 0x2 message type.")));
                return false;
            }
        }
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer, IWebSocketReceiveResult result)
        {
            try
            {
                var str = Encoding.GetString(rxBuffer);
                WebSocketMessage? x = APIUtils.DeserializeWebsocketMessage(str);
                if (x != null)
                {
                    switch (x.type)
                    {
                        case MessageType.ClientWelcomeReply:
                            var msg = (ClientWelcomeReply)x;
                            if (string.IsNullOrEmpty(msg.Token))
                            {
                                await SendAsync(context, Serialize(new AuthenticationFail("Null authentication token was provided while authenticating to websocket")));
                            }
                            else
                            {
                                User? currentUser = null;
                                //verify token
                                foreach (var item in Configuration.Instance.Tokens)
                                {
                                    if (item.Key == msg.Token)
                                    {
                                        foreach (var user in Configuration.Instance.Users)
                                        {
                                            if (user.Username == item.Value)
                                            {
                                                currentUser = user;
                                                break;
                                            }
                                        }
                                        break;
                                    }
                                }

                                if (currentUser == null)
                                {
                                    await SendAsync(context, Serialize(new AuthenticationFail("Invaild authentication token")));
                                }
                                else
                                {
                                    if (AuthenticatedSessions.TryAdd(context.Session.Id, currentUser)) { }

                                    await SendAsync(context, Serialize(BuildAuthenticationOK()));
                                }
                            }
                            break;
                        case MessageType.MusicVolumeChange:
                            if (await IsAuthed(context))
                            {
                                var musicMsg = (MusicPlayerVolumeChange)x;
                                if (musicMsg.MusicVolume != -1)
                                {
                                    // update volume
                                    Console.WriteLine("websocket: update music volume to " + musicMsg.MusicVolume);
                                    MusicPlayer.MusicVol = musicMsg.MusicVolume;
                                }
                                else
                                {
                                    // client is requesting current music volume
                                    await SendAsync(context, Serialize(new MusicPlayerVolumeChange() { MusicVolume = MusicPlayer.MusicVol }));
                                }
                            }
                            break;
                        case MessageType.AnncVolumeChange:
                            if (await IsAuthed(context))
                            {
                                var musicMsg = (AnncPlayerVolumeChange)x;
                                if (musicMsg.AnncVolume != -1)
                                {
                                    // update volume
                                    Console.WriteLine("websocket: update annc volume to " + musicMsg.AnncVolume);
                                    MusicPlayer.Anncvol = musicMsg.AnncVolume;
                                }
                                else
                                {
                                    // client is requesting current music volume
                                    await SendAsync(context, Serialize(new AnncPlayerVolumeChange() { AnncVolume = MusicPlayer.Anncvol }));
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("websocket error: " + ex.ToString());
            }
        }

        private SystemStateChange BuildSystemStateChange(bool beep)
        {
            SystemStateChange state = new()
            {
                IsAlarmState = Configuration.Instance.SystemAlarmState,
                IsSystemArmed = Configuration.Instance.SystemArmed,
                SystemTimer = Configuration.Instance.Timer,
                IsEntryDelay = Configuration.Instance.InEntryDelay,
                IsCountdownInProgress = beep,
                IsReady = ZoneController.IsReady
            };
            return state;
        }
        private AuthenticationOK BuildAuthenticationOK()
        {
            return new AuthenticationOK(BuildSystemStateChange(false), BuildZoneUpdate());
        }

        private ZoneUpdate BuildZoneUpdate()
        {
            ZoneUpdate zones = new();
            JsonZoneWithReady[] zonesContent = new JsonZoneWithReady[Configuration.Instance.Zones.Count];

            int i = 0;
            foreach (var item in Configuration.Instance.Zones)
            {
                JsonZoneWithReady obj = new JsonZoneWithReady();
                obj.idx = item.Value.ZoneNumber;
                obj.name = item.Value.Name;
                obj.type = item.Value.Type;
                obj.ready = ZoneController.ZoneStates[item.Key] == PinValue.Low;
                zonesContent[i++] = obj;
            }

            zones.Zones = zonesContent;

            return zones;
        }
        protected override async Task OnClientConnectedAsync(IWebSocketContext context)
        {
            await SendAsync(context, Serialize(new Hello()));
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            if (AuthenticatedSessions.ContainsKey(context.Session.Id))
                AuthenticatedSessions.Remove(context.Session.Id);
            return Task.CompletedTask;
        }
        private async Task SendToAll(WebSocketMessage toSerialize)
        {
            foreach (var context in ActiveContexts)
            {
                if (AuthenticatedSessions.ContainsKey(context.Session.Id))
                {
                    if (AuthenticatedSessions[context.Session.Id] != null)
                    {
                        await SendAsync(context, Serialize(toSerialize));
                    }
                }
            }
        }

        public async void SendZoneStateChange()
        {
            await SendToAll(BuildZoneUpdate());
        }
        internal async Task SendSysStateChangeToAll(bool beep)
        {
            await SendToAll(BuildSystemStateChange(beep));
        }

        public async void SystemManager_AnnounceAlarm(int zone)
        {
            // todo: include zone
            await SendToAll(BuildSystemStateChange(false));
        }

        public async void SystemManager_OnSystemDisarm(object? sender, EventArgs e)
        {
            await SendSysStateChangeToAll(false);
        }

        private void SystemManager_OnZoneUpdate(bool single, int zone, string name, Modules.NXDisplay.ZoneState ready)
        {
            SendZoneStateChange();
        }

        private async void SystemManager_SysTimerEvent(bool entry, int timer)
        {
            await SendSysStateChangeToAll(true);
        }
    }
}
