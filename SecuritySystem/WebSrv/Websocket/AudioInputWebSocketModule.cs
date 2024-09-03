using EmbedIO.WebSockets;
using MHSApi.WebSocket.AudioIn;
using SecuritySystem.Alsa;
using SecuritySystem.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem.WebSrv.Websocket
{
    public class AudioInputWebSocketModule : WebSocketModule
    {
        readonly Dictionary<string, User?> AuthenticatedSessions = new();
        private static UnixPCMDevice? PcmDevice;

        public AudioInputWebSocketModule(string urlPath)
         : base(urlPath, true)
        {

        }

        protected override async Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            if (await IsAuthed(context))
            {
                if (PcmDevice != null)
                {
                    PcmDevice.Close();
                    PcmDevice = null;
                }
            }

            await base.OnClientDisconnectedAsync(context);
        }
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            if (buffer.Length == 0)
            {
                await SendCommandAsync(context, AudioInMsgType.CmdError);
                return;
            }

            var cmd = (AudioInMsgType)buffer[0];

            if (cmd != AudioInMsgType.DoAuth && !await IsAuthed(context))
            {
                await SendCommandAsync(context, AudioInMsgType.AuthFail);
                return;
            }

            if (cmd == AudioInMsgType.DoAuth)
            {
                try
                {
                    var tokenLen = buffer[1];
                    var token = Encoding.ASCII.GetString(buffer.AsMemory().Slice(2, tokenLen).ToArray());

                    User? currentUser = null;
                    foreach (var item in Configuration.Instance.Tokens)
                    {
                        if (item.Key == token)
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
                        await SendCommandAsync(context, AudioInMsgType.AuthFail);
                    }
                    else
                    {
                        // authentication was OK
                        if (AuthenticatedSessions.TryAdd(context.Session.Id, currentUser)) { }

                        await SendCommandAsync(context, AudioInMsgType.OK);
                    }
                }
                catch
                {
                    await SendCommandAsync(context, AudioInMsgType.CmdError);
                }
            }
            else if (cmd == AudioInMsgType.WritePcm)
            {
                var pcm = buffer.Skip(1).ToArray(); // Skip command byte
                if (PcmDevice == null)
                {
                    await SendCommandAsync(context, AudioInMsgType.CmdError);
                }
                else
                {
                    if (OperatingSystem.IsLinux())
                        PcmDevice.Write(new MemoryStream(pcm));
                    else
                        Console.WriteLine("Simulating WritePcm");
                }
            }
            else if (cmd == AudioInMsgType.OpenAudioDevice)
            {
                if (PcmDevice != null)
                    await SendCommandAsync(context, AudioInMsgType.CmdError);
                else
                {
                    BinaryReader br = new BinaryReader(new MemoryStream(buffer));
                    br.ReadByte(); // skip command byte

                    int sampleRate = br.ReadInt32();
                    int bits = br.ReadInt32();
                    int blockAlignment = br.ReadInt32();

                    PcmDevice = new UnixPCMDevice(new Iot.Device.Media.SoundConnectionSettings());

                    if (OperatingSystem.IsLinux())
                        PcmDevice.Open((ushort)bits, (uint)sampleRate, (ushort)blockAlignment);
                    else
                        Console.WriteLine("Simulating OpenAudioDevice");
                    MusicPlayer.StartAsyncMicAnnc();
                    await SendCommandAsync(context, AudioInMsgType.OK);
                }
            }
            else if (cmd == AudioInMsgType.CloseAudioDevice)
            {
                if (PcmDevice == null)
                    await SendCommandAsync(context, AudioInMsgType.CmdError);
                else
                {
                    MusicPlayer.StopAsyncMicAnnc();
                    PcmDevice.Close();
                    PcmDevice = null;
                    await SendCommandAsync(context, AudioInMsgType.OK);
                }
            }
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
                    await SendCommandAsync(context, AudioInMsgType.AuthFail);
                    return false;
                }
            }
            else
            {
                await SendCommandAsync(context, AudioInMsgType.NoAuth);
                return false;
            }
        }

        private async Task SendCommandAsync(IWebSocketContext context, AudioInMsgType authFail)
        {
            await SendAsync(context, [(byte)authFail]);
        }
    }
}