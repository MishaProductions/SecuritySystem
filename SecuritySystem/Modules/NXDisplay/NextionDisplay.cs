using MHSApi.API;
using Newtonsoft.Json.Linq;
using SecuritySystem.DeviceSubsys;
using SecuritySystem.Utils;
using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace SecuritySystem.Modules.NXDisplay
{
    public class NextionDisplay(string comport) : Module
    {
        private Thread? packetReader;
        private bool KeypadSendCommands = true;
        private string? comport = comport;
        public static bool DoorChime = true;
        public bool ShufflePlaylist = false;
        public bool LoopPlaylist = false;
        private SerialPort? _port;
        public int UpdateProgress = 0;
        public bool UpdateFail = false;
        public bool UpdateFinish = false;
        public bool UpdateInProgress = false;
        public string UpdateProgressString = "No firmware update in progress";
        public bool WlanAuthenticated = false;
        public bool IsSleepMode = false;
        public bool WarningFlash = false;
        public bool ZoneFlash = false;
        public bool ImageState = false;
        private DateTime lastDisplayPing;
        public int ImageId = 2;
        string currentPage = "pageHome";
        private int FwUpdateBuadRate = 256000; // note: 512000 or higher didnt work for me
        private int NormalBaudRate = 115200;
        private SerialPort? SerialPort
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
                if (_port == null)
                {
                    Console.WriteLine("set port to null");
                }
            }
        }
        #region Module
        public override void OnRegister()
        {
            Init();

            MusicPlayer.OnMusicStop += MusicPlayer_OnMusicStop;
            MusicPlayer.OnAnncStop += MusicPlayer_OnAnncStop;
            MusicPlayer.OnMusicVolumeChanged += MusicPlayer_OnMusicVolumeChanged;
            MusicPlayer.OnAnncVolumeChanged += MusicPlayer_OnAnncVolumeChanged;

            SystemManager.OnZoneUpdate += SystemManager_OnZoneUpdate;
            SystemManager.OnAlarm += SystemManager_OnAlarm;
            SystemManager.OnSystemDisarm += SystemManager_OnSystemDisarm;
            SystemManager.OnSysTimerEvent += SystemManager_OnSysTimerEvent;
            SystemManager.OnInactiveZone += SystemManager_OnInactiveZone;

            TroubleManager.OnNewCondition += TroubleManager_OnNewCondition;
            TroubleManager.OnConditionCleared += TroubleManager_OnClearCondition;
        }

        public override void OnUnregister()
        {
            KeypadSendCommands = false;
            SerialPort?.Close();
            SerialPort = null;


            MusicPlayer.OnMusicStop -= MusicPlayer_OnMusicStop;
            MusicPlayer.OnAnncStop -= MusicPlayer_OnAnncStop;

            SystemManager.OnZoneUpdate -= SystemManager_OnZoneUpdate;
            SystemManager.OnAlarm -= SystemManager_OnAlarm;
            SystemManager.OnSystemDisarm -= SystemManager_OnSystemDisarm;
            SystemManager.OnSysTimerEvent -= SystemManager_OnSysTimerEvent;
        }

        private void LcdBoot()
        {
            if (!KeypadSendCommands)
                return;

            Console.WriteLine("NextionDisplay: initializing");

            SetCmptVal("pageBoot.j0", "100");
            SendCommand("bkcmd=2");

            SetPage("pageAppInit");
            SetCmptText("t0", "Display Initializing...");
            SetCmptText("t1", "Downloading Configuration");
        }

        private void Init()
        {
            SerialPort = new SerialPort(comport, NormalBaudRate);
            SerialPort.Open();
            packetReader = new(PacketRxThread);
            packetReader.Start();
            LcdBoot();
            InitKeypad();

            var aTimer = new System.Timers.Timer(1000 * 60 * 30); // update every 30 minutes (in milliseconds)
            aTimer.Elapsed += new ElapsedEventHandler(HandleWeatherTimer);
            aTimer.Start();

            var warningT = new System.Timers.Timer(1000);
            warningT.Elapsed += new ElapsedEventHandler(HandleWarningFlash);
            warningT.Start();
        }
        #endregion
        #region Event handlers
        private void HandleWarningFlash(object? sender, ElapsedEventArgs e)
        {
            if (currentPage != "pageHome") return;

            if (WarningFlash)
            {
                if (ImageState)
                {
                    SetCmptVisible("pStatus", false);
                }
                else
                {
                    SetCmptVisible("pStatus", true);
                }
            }

            if (ZoneFlash)
            {
                bool hasTrouble = false;
                int i = 1;
                foreach (var item in ZoneController.ZoneStates)
                {
                    if (ZoneController.IsTroubleSet(item))
                    {
                        hasTrouble = true;
                        SetZoneColor(i, item, !ImageState);
                    }
                    i++;
                }

                ZoneFlash = hasTrouble;
            }

            ImageState = !ImageState;
        }
        private void HandleWeatherTimer(object? sender, ElapsedEventArgs e)
        {
            RefreshWeather();

            if (currentPage == "pageHome")
                UpdateTroubleCondition();
        }
        private void MusicPlayer_OnAnncStop(object? sender, EventArgs e)
        {
            if (currentPage == "pageMusic")
            {
                SendCommand("vis bPlayAnnc,1");
            }
        }
        private void MusicPlayer_OnMusicStop(object? sender, EventArgs e)
        {
            if (currentPage == "pageMusic")
            {
                SendCommand($"tCurrentSong.txt=\"Current file: none\"");
            }
        }

        private void MusicPlayer_OnAnncVolumeChanged(object? sender, EventArgs e)
        {
            if (currentPage == "pageMusic")
            {
                SendCommand("h2.val=" + MusicPlayer.Anncvol);
            }
        }

        private void MusicPlayer_OnMusicVolumeChanged(object? sender, EventArgs e)
        {
            if (currentPage == "pageMusic")
            {
                SendCommand("h1.val=" + MusicPlayer.MusicVol);
            }
        }

        public void PlayMusic(string path)
        {
            // Disabled as the nextion display speakers are very bad.
            /*if (currentPage != "pageHome") return;
            string properPath = "sd0" + path.Replace(".mp3", ".wav").Replace(".flac", ".wav");
            Console.WriteLine("Play music " + properPath);
            SendCommand("pageHome.wavMusic.path=\"" + properPath + "\"");
            SendCommand("pageHome.wavMusic.en=1");*/
        }
        public void StopMusic()
        {
            //SendCommand("pageHome.wavMusic.en=0");
        }
        public void PlayAnnc(string path)
        {
            /*if (currentPage != "pageHome") return;
            string properPath = "sd0" + path.Replace(".mp3", ".wav").Replace(".flac", ".wav");
            Console.WriteLine("Play annc " + properPath);
            SendCommand("pageHome.wavAnnc.path=\"" + properPath + "\"");
            SendCommand("pageHome.wavAnnc.en=1");*/
        }
        public void StopAnnc()
        {
            //SendCommand("pageHome.wavAnnc.en=0");
        }
        private void SystemManager_OnZoneUpdate(bool single, int zone, string name, ZoneState ready)
        {
            if (single)
            {
                SetZoneStatus(zone, name, ready);

                if (ready == ZoneState.NotReady)
                {
                    if (DoorChime)
                    {
                        ButtonBeep();

                        // wake screen
                        SendCommand("click bWait,0");
                        SendCommand("click bWait,1");
                    }
                }
            }
            else
            {
                foreach (var item in Configuration.Instance.Zones)
                {
                    if (item.Value.Type == ZoneType.None)
                    {
                        SetZoneStatus(item.Value.ZoneNumber + 1, item.Value.Name, ZoneState.Unconfigured);
                    }
                    else
                    {
                        SetZoneStatus(item.Value.ZoneNumber + 1, item.Value.Name, ZoneController.ZoneStates[item.Key]);
                    }
                }
            }

            UpdateStatusText();
        }
        private void SystemManager_OnAlarm(int alarmZone)
        {
            // todo: play some annoying sound
            UpdateStatusText();
            SendCommand("play 0,5,1"); //channel 1, play resource 1, no loop
        }
        private void SystemManager_OnSystemDisarm(object? sender, EventArgs e)
        {
            // show main view
            UpdateStatusText();

            SendCommand("audio1=0");
        }
        private void SystemManager_OnSysTimerEvent(bool entry, int timer)
        {
            AlarmBeep();
            UpdateStatusText();
        }
        private void SystemManager_OnInactiveZone(int idx, TimeSpan sinceInactive)
        {
            TroubleBeep();
            UpdateStatusText();
            UpdateTroubleCondition();
        }
        private void TroubleManager_OnNewCondition(TroubleCondition condition)
        {
            TroubleBeep();
            UpdateStatusText();
            UpdateTroubleCondition();
        }
        private void TroubleManager_OnClearCondition(TroubleCondition condition)
        {
            UpdateStatusText();
            UpdateTroubleCondition();
        }
        #endregion
        #region Firmware update
        private static uint ReadFileSize(byte[] firmware)
        {
            BinaryReader br = new(new MemoryStream(firmware));
            br.BaseStream.Position = 0x3C;
            return br.ReadUInt32();
        }
        private byte[]? FirmwareData;
        public void UpdateFirmware(byte[] firmware)
        {
            SendCommand("play 0,4,0"); //channel 1, play resource 2, no loop
            //disable sending of commands and disable packetreader thread
            KeypadSendCommands = false;
            SerialPort?.Close();
            SerialPort = null;

            FirmwareData = firmware;

            UpdateProgressString = "Initializing Update...";
            DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, 0);

            Console.WriteLine("Updating nextion display at " + comport);
            if (comport == null)
            {
                throw new Exception("comport cannot be NULL");
            }
            //wait for other thread to die
            Thread.Sleep(5000);
            Thread worker = new(FirmwareUpdateThread);
            worker.Start();
        }
        private void FirmwareUpdateThread()
        {
            if (FirmwareData == null) throw new Exception("firmware to upload cannot be null");

            // The code here is based on https://github.com/MMMZZZZ/Nexus/blob/master/Nexus.py
            SerialPort = new SerialPort(comport, NormalBaudRate)
            {
                // This is very important when using any SerialPort methods that make use of string or char types
                // Byte values will be truncated to byte values allowed in codepage
                // \xff becomes \x3f for ASCII which is the default encoding
                // https://social.msdn.microsoft.com/Forums/en-US/efe127eb-b84b-4ae5-bd7c-a0283132f585/serial-port-sending-8-bit-problem?forum=Vsexpressvb
                Encoding = Encoding.GetEncoding(28591),
                ReadTimeout = 10000
            };
            SerialPort.Open();

            if (SerialPort == null)
            {
                Console.WriteLine("the serial port is null after opening it");
                return;
            }
            UpdateInProgress = true;
            UpdateProgressString = "Communication with keypad in progress";
            DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, 0);

            //SendCommand("DRAKJHSUYDGBNCJHGJKSHBDN");
            SendCommand("");
            SendCommand("connect");

            // Read connect handshake
            var comok = ReadPacket();

            // Example response: comok 2,1793-0,NX8048P070_011C,163,10501,C68A340174C5D077,128974848-0
            Console.WriteLine(Encoding.ASCII.GetString(comok, 0, comok.Length - 3));
            if (comok[0] != 'c')
            {
                UpdateProgressString = "Communication with keypad failed: incorrect response 1";
                UpdateFail = true;
                Console.WriteLine("FW update failed: incorrect data " + Encoding.ASCII.GetString(comok));
                UpdateInProgress = false;
                return;
            }
            SendCommand("");
            var fwsize = ReadFileSize(FirmwareData);
            Console.WriteLine($"firmware size: " + fwsize);

            //For some reason the first command after self.connect() always fails. Can be anything.
            SendCommand("bs=42");
            SendCommand("dim=15");
            SendCommand("sleep=0");
            Console.WriteLine("Initiating upload...");

            // Use higher buad rate for firmware upload
            SendCommand($"whmi-wris {fwsize}," + FwUpdateBuadRate + ",1");

            SerialPort.Close();
            SerialPort = new SerialPort(comport, FwUpdateBuadRate)
            {
                // This is very important when using any SerialPort methods that make use of string or char types
                // Byte values will be truncated to byte values allowed in codepage
                // \xff becomes \x3f for ASCII which is the default encoding
                // https://social.msdn.microsoft.com/Forums/en-US/efe127eb-b84b-4ae5-bd7c-a0283132f585/serial-port-sending-8-bit-problem?forum=Vsexpressvb
                Encoding = Encoding.GetEncoding(28591),
                ReadTimeout = 10000
            };
            SerialPort.Open();

            UpdateProgressString = "Waiting for keypad";
            DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, 0);
            var lastval = 0;
            try
            {
                while ((lastval = SerialPort.ReadByte()) != 0x05)
                {
                    Console.WriteLine("read " + lastval);
                }
            }
            catch (Exception ex)
            {
                //timeout
                Console.WriteLine(ex.Message);
            }
            if (lastval != 0x05)
            {
                Console.WriteLine("Expected acknowledge (0x05) but got " + lastval);
                return;
            }
            Console.WriteLine("Handshake result:" + lastval);
            var blockSize = 4096;
            var remainingBlocks = (int)Math.Ceiling((double)(fwsize / blockSize));
            int blocksSent = 0;
            BinaryReader fw = new(new MemoryStream(FirmwareData));
            UpdateProgressString = "Sending data";
            DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, 0);
            while (remainingBlocks != 0)
            {
                var b = fw.ReadBytes(blockSize);
                SerialPort.Write(b, 0, b.Length);

                remainingBlocks--;
                blocksSent++;


                Console.WriteLine("proceed start");
                var proceed = SerialPort.ReadByte();
                Console.WriteLine("proceed end");
                if (proceed == 0x08)
                {
                    //NXSKIP
                    byte[] buf = new byte[4];
                    SerialPort.Read(buf, 0, 4);
                    var offset = BitConverter.ToInt32(buf);

                    if (offset != 0)
                    {
                        //# A value of 0 doesn't mean "seek to position 0" but "don't seek anywhere".
                        var org = fw.BaseStream.Position;
                        var jumpSize = offset - fw.BaseStream.Position;
                        fw.BaseStream.Position = offset;
                        //We need to round up if we need to send part of a chunk
                        remainingBlocks = (int)Math.Ceiling((double)((fwsize - offset) / blockSize)) + 1;
                        Console.WriteLine($"Skipped {jumpSize} bytes,org={org} and there are now {remainingBlocks} remaining blocks");
                    }
                }
                else
                {
                    // if not a:
                    // a = self.ser.read_until(self.NXACK)

                    if (proceed == 0)
                    {
                        var lastval2 = 0;
                        try
                        {
                            while ((lastval2 = SerialPort.ReadByte()) != 0x05)
                            {

                            }
                        }
                        catch (Exception ex)
                        {
                            //timeout
                            Console.WriteLine(ex.Message);
                        }
                        if (lastval2 != 0x05)
                        {
                            Console.WriteLine("Expected acknowledge (0x05) but got " + lastval2);
                        }
                    }
                }

                var progress = Math.Ceiling((double)(100 * fw.BaseStream.Position / fwsize));

                DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, (int)progress);
                UpdateProgress = (int)progress;
                Console.WriteLine($"progress: {progress}% RemainingBlocks:" + remainingBlocks + ",sent=" + blocksSent);

                if (remainingBlocks == 0 && UpdateProgress != 100)
                {
                    Console.WriteLine("!!!ERROR: UPDATE SIZE MISCALCULATION!!! Increasing size of remainingBlocks");
                    remainingBlocks++;
                }
            }
            SerialPort.Close();
            //We have finished
            if (UpdateProgress == 100)
            {
                UpdateProgressString = "Finished.";
                UpdateFinish = true;
                Console.WriteLine("Firmware upgrade finished. Using " + comport);
                KeypadSendCommands = true;
                UpdateInProgress = false;
                //wait for the keypad to fully reboot
                Thread.Sleep(2000);
                Init();
            }
            else
            {
                Console.WriteLine("update fail: not actually finished");
            }
        }
        #endregion
        private void InitKeypad(bool navigate = true)
        {
            if (!KeypadSendCommands)
                return;

            currentPage = "pageHome";
            if (navigate)
            {
                SetPage("pageHome");
            }

            //Sync RTC
            SendCommand($"rtc0={DateTime.Now.Year}");
            SendCommand($"rtc1={DateTime.Now.Month}");
            SendCommand($"rtc2={DateTime.Now.Day}");
            SendCommand($"rtc3={DateTime.Now.Hour}");
            SendCommand($"rtc4={DateTime.Now.Minute}");
            SendCommand($"rtc5={DateTime.Now.Second}");
            SetCmptPic("pNetwork", 1);
            SetCmptVisible("bShowT", false);
            SendCommand($"thsp=0");
            RefreshWeather();
            SystemManager_OnZoneUpdate(false, 0, "", ZoneState.Unconfigured);

            SendCommand("audio0=0");
            SendCommand("audio1=0");

            UpdateTroubleCondition();
        }

        private void UpdateTroubleCondition()
        {
            Console.WriteLine("UpdateTroubleCondition, count=" + TroubleManager.ActiveConditions.Count);
            if (TroubleManager.ActiveConditions.Count == 0)
            {
                WarningFlash = false;
                SetCmptVisible("bShowT", false);
                SetCmptVisible("pStatus", false);
            }
            else
            {
                WarningFlash = true;
                SetCmptPic("pageHome.pStatus", 9);
                SetCmptVisible("bShowT", true);
                SetCmptVisible("pStatus", true);
            }
        }

        private void HandleShowTrouble()
        {
            if (TroubleManager.ActiveConditions.Count == 0)
            {
                SetPage("pageHome");
                UpdateTroubleCondition();
                return;
            }

            var t = TroubleManager.ActiveConditions[0];
            Console.WriteLine("troubleLog count: " + TroubleManager.ActiveConditions.Count);

            ShowPrompt(t.Title, t.GetDescription(), true, 9,
        "", false, null,
        "", false, null,
        TroubleManager.ActiveConditions.Count > 1 ? "Next" : "Done", true, () =>
        {
            TroubleManager.RemoveTroubleCondition(t);
            Console.WriteLine("go to next log");
            HandleShowTrouble();
        }, 21152);
        }

        public async void RefreshWeather()
        {
            if (IsSleepMode) return;
            if (currentPage != "pageHome")
            {
                Console.WriteLine("[weather] wrong page: " + currentPage);
                return;
            }
            if (!KeypadSendCommands)
                return;
            SetCmptText("tWeather", await WeatherService.GetWeather());
        }
        public void UpdateStatusText()
        {
            if (!KeypadSendCommands)
                return;
            bool ready = true;
            //We need to verify that all zones are indeed ready (never trust the client!)
            foreach (var item in Configuration.Instance.Zones)
            {
                if (!ZoneController.IsZoneReady(item.Key))
                {
                    if (item.Value.Type != ZoneType.None)
                    {
                        //umm
                        ready = false;
                    }
                }
            }
            string text;
            if (Configuration.Instance.SystemArmed)
            {
                text = "System armed ";
                if (Configuration.Instance.InEntryDelay)
                {
                    text = "Alarm in " + Configuration.Instance.Timer;
                }
                else if (Configuration.Instance.InExitDelay)
                {
                    text = "System arming in " + Configuration.Instance.Timer;
                }
                else if (Configuration.Instance.SystemAlarmState)
                {
                    text = "ALARM";
                }
            }
            else
            {
                if (ready)
                {
                    text = "System ready";
                }
                else
                {
                    text = "System not ready";
                }
            }

            SendCommand($"pageHome.stat.txt=\"{text}\"");

            if (Configuration.Instance.SystemArmed)
            {
                //set arm button text to disarm system
                SendCommand($"pageHome.bIsSystemArmed.val=1");
                SendCommand($"tsw bDArmSys,1");
                SendCommand($"bDArmSys.txt=\"Disarm\"");
                SendCommand($"vis bSettings,0");
                SetCmptVisible("bSettingsBg", false);
                SendCommand($"vis bMusicLoop,0");
                SetCmptVisible("bMusicLoopBg", false);
                SendCommand($"vis b0,0");
            }
            else
            {
                SetCmptVisible("bSettingsBg", true);
                SendCommand($"vis bSettings,1");
                SetCmptVisible("bMusicLoopBg", true);
                SendCommand($"vis bMusicLoop,1");

                SendCommand($"vis b0,1");
                SendCommand($"bDArmSys.txt=\"Arm System\"");
                SendCommand($"pageHome.bIsSystemArmed.val=0");
                if (!ready)
                {
                    //disable arm button when not ready
                    SendCommand($"tsw bDArmSys,0");

                    //just in case (click the cancel button)
                    HideHud();
                }
                else
                {
                    //enable arm button when ready
                    SendCommand($"tsw bDArmSys,1");
                }
            }
        }
        private void SetZoneStatus(int zone, string name, ZoneState ready)
        {
            if (!KeypadSendCommands)
                return;
            Console.WriteLine($"NextionDisplay: zone #{zone} - {name} is {ready}");

            //set zone name
            SendCommand($"z{zone}.txt=\"#{zone}: {name}\"");

            SetZoneColor(zone, ready, true);

            if (ZoneController.IsTroubleSet(ready))
            {
                ZoneFlash = true;
            }

            UpdateStatusText();
        }

        private void SetZoneColor(int zone, ZoneState ready, bool showYellow)
        {
            if (ready == ZoneState.Unconfigured)
            {
                SendCommand($"z{zone}.pco=50712");
            }
            else if (ready == ZoneState.Ready)
            {
                SendCommand($"z{zone}.pco=2016");
            }
            else
            {
                //not ready
                SendCommand($"z{zone}.pco=63488");
            }

            if (ZoneController.IsTroubleSet(ready) && showYellow)
            {
                SendCommand($"z{zone}.pco=65504");
            }
        }
        internal void SendCommand(string command)
        {
            if (SerialPort == null)
                throw new Exception("Init() method not called because NextionDisplayController.SerialPort is null");

            byte[] pkt = new byte[command.Length + 3];
            int i = 0;
            foreach (var item in Encoding.ASCII.GetBytes(command))
            {
                pkt[i++] = item;

            }
            pkt[^1] = 0xFF;
            pkt[^2] = 0xFF;
            pkt[^3] = 0xFF;
            SerialPort.Write(pkt, 0, pkt.Length);
        }
        private byte[] ReadPacket()
        {
            if (SerialPort == null)
                throw new Exception("Init() method not called because NextionDisplayController.SerialPort is null");

            //read packet result
            byte aTermBytes = 0;
            List<byte> packet = [];
            while (true)
            {
                byte b = (byte)SerialPort.ReadByte();
                packet.Add(b);
                if (b == 0xFF)
                {
                    aTermBytes++;
                }
                else
                {
                    aTermBytes = 0;
                }
                if (aTermBytes >= 3)
                {
                    return [.. packet];
                }
            }
        }
        private void PacketRxThread(object? obj)
        {
            while (KeypadSendCommands)
            {
                try
                {
                    var p = ReadPacket();
                    //https://nextion.tech/instruction-set/
                    switch (p[0])
                    {
                        case 0x00:
                            Console.WriteLine("Instruction sent is invaild");
                            break;
                        case 0x01:
                            //sucess
                            break;
                        case 0x02:
                            Console.WriteLine("Invaild Component ID or name was used");
                            break;
                        case 0x03:
                            Console.WriteLine("Invaild Page ID");
                            break;
                        case 0x04:
                            Console.WriteLine("Invaild Picture ID");
                            break;
                        case 0x05:
                            Console.WriteLine("Invaild Font ID");
                            break;
                        case 0x06:
                            Console.WriteLine("Invaild File Operation");
                            break;
                        case 0x09:
                            Console.WriteLine("Invaild CRC");
                            break;
                        case 0x11:
                            Console.WriteLine("Invalid Baud rate Setting");
                            break;
                        case 0x12:
                            Console.WriteLine("Invalid Waveform ID or channel number");
                            break;
                        case 0x1A:
                            Console.WriteLine("Invalid Variable name or attribute");
                            break;
                        case 0x1B:
                            Console.WriteLine("Invalid Variable Operation");
                            break;
                        case 0x1C:
                            Console.WriteLine("Assignment failed to assign");
                            break;
                        case 0x1D:
                            Console.WriteLine("EEPROM Operation failed");
                            break;
                        case 0x1E:
                            Console.WriteLine("Invalid Quantity of Parameters");
                            break;
                        case 0x1F:
                            Console.WriteLine("IO Operation failed");
                            break;
                        case 0x20:
                            Console.WriteLine("Escape Character Invalid");
                            break;
                        case 0x23:
                            Console.WriteLine("Variable name too long");
                            break;
                        case 0x86:
                            Console.WriteLine("keypad: Entered sleep mode");
                            IsSleepMode = true;
                            break;
                        case 0x87:
                            //Nextion ready event
                            Console.WriteLine("keypad: Exited sleep mode");
                            IsSleepMode = false;
                            RefreshWeather();
                            break;
                        case 0x88:
                            //Nextion ready event
                            Console.WriteLine("Warning: keypad restarted");
                            TroubleManager.InsertTroubleCondition(DispayUnexpectedResetTroubleCondition.Create());

                            InitKeypad();
                            break;
                        case 0x65:
                            byte page = p[1];
                            byte componentID = p[2];
                            byte @event = p[3];
                            Console.WriteLine($"Touch event at page {page}, cmpt: {componentID}, evnt: {@event}");
                            HandleTouchEvent(page, componentID, @event);
                            break;
                        case 0x67:
                            // touch cordinate when awake
                            break;
                        case 0x68:
                            // touch cordinate when asleep
                            SendCommand("click bWait,1");
                            IsSleepMode = false;
                            break;
                        case 0x70:
                            //Text sent
                            var x = Encoding.ASCII.GetString(p, 1, p.Length - 4);

                            HandleStringCommand(x);
                            break;
                        default:
                            Console.WriteLine("Warning: unknown packet 0x" + p[0].ToString("X2"));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("packet reader thread error: " + ex.ToString());
                }
            }

        }
        private static void HandleTouchEvent(byte page, byte componentID, byte p_event)
        {
            if (page == 7) //music page
            {
                MusicPlayer.PlayAnnc(componentID - 1);
            }
        }
        private void SetCmptText(string name, string text)
        {
            SendCommand($"{name}.txt=\"{text}\"");
        }
        private void SetCmptVal(string name, string val)
        {
            SendCommand($"{name}.val={val}");
        }
        private void SetCmptVal(string name, int val)
        {
            SendCommand($"{name}.val={val}");
        }
        private void SetCmptVal(string name, bool val)
        {
            SendCommand($"{name}.val={(val ? 1 : 0)}");
        }
        private void SetCmptPic(string name, string val)
        {
            SendCommand($"{name}.val={val}");
        }
        // TODO: vis does not support page!
        private void SetCmptVisible(string name, bool val)
        {
            int rawValue = val == true ? 1 : 0;
            SendCommand($"vis {name},{rawValue}");
        }
        private void SetDispPage(string name)
        {
            SendCommand($"page {name}");
            currentPage = name;
        }
        private void SetCmptPic(string name, int picIndex)
        {
            SendCommand($"{name}.pic={picIndex}");
        }
        private void SetCmptBGColor(string name, int bgColor)
        {
            SendCommand($"{name}.bco={bgColor}");
        }

        private readonly string[] WeekLabels = ["t2", "t3", "t4", "t5", "t6", "t7", "t8"];
        private readonly string[] TempLabels = ["t9", "t10", "t11", "t12", "t13", "t14", "t15"];
        private readonly string[] WeatherLabels = ["t16", "t17", "t18", "t18", "t20", "t21", "t22"];
        private readonly string[] WeatherPictures = ["p1", "p2", "p3", "p4", "p5", "p6", "p7"];
        private readonly string[] WeekLabelText = ["Sat", "Mon", "Tue", "Wen", "Thu", "Fri", "Sat"];
        private bool PromptOpen = false;

        private Action? PromptB1Action;
        private Action? PromptB2Action;
        private Action? PromptB3Action;
        private bool ShowedFirmwareMismatch = false;

        private void ShowPrompt(string title, string message, bool iconVisible, int icon, string b1Text, bool b1Visible, Action? b1Click, string b2Text, bool b2Visible, Action? b2Click, string b3Text, bool b3Visible, Action? b3Click, int bg = 16904)
        {
            SendCommand("click bWait,0");
            SendCommand("click bWait,1");

            SetDispPage("pageSysMsg");

            // setup basics
            SetCmptText("pageSysMsg.tMsgTitle", title);
            SetCmptText("pageSysMsg.tMsgTxt", message);
            SetCmptVisible("pageSysMsg.pMsgPic", iconVisible);
            SetCmptBGColor("pageSysMsg.tBg", bg);

            if (iconVisible)
                SetCmptPic("pageSysMsg.pMsgPic", icon);

            // update button text
            SetCmptText("pageSysMsg.b0", b1Text);
            SetCmptText("pageSysMsg.b1", b2Text);
            SetCmptText("pageSysMsg.b2", b3Text);

            // update button visibility
            SetCmptVisible("b0", b1Visible);
            SetCmptVisible("b1", b2Visible);
            SetCmptVisible("b2", b3Visible);

            // update button handlers
            PromptB1Action = b1Click;
            PromptB2Action = b2Click;
            PromptB3Action = b3Click;
        }
        // Shows a warning with OK button
        private void ShowSimpleWarning(string title, string message, Action onOk)
        {
            ShowPrompt(title, message, true, 9,
            "", false, null,
            "OK", true, onOk,
            "", false, null, 21152
            );
        }
        private void ShowNotImpl(string detail)
        {
            ShowSimpleWarning("Communication Fail", "The requested action is not\r\nimplemented. Press OK to reload.\r\nDetails:" + detail, () =>
            {
                PromptOpen = false;
                InitKeypad();
            });
        }
        private void ShowBadOperation(string detail)
        {
            ShowSimpleWarning("Communication Fail", "The requested action is not currently\r\navailable. Press OK to reload. Details:\r\n" + detail, () =>
            {
                PromptOpen = false;
                InitKeypad();
            });
        }
        private async void InitWeatherPage(bool show7Day)
        {
            if (currentPage != "pageWeather")
            {
                Console.WriteLine("nextion: attempted to init weather page when not weather page!");
                return;
            }
            //SendCommand("play 0,5,1");
            SetCmptVisible("j0", true);
            SetCmptVisible("tLdr", true);
            SetCmptText("tLdr", "Downloading Location data");
            SetCmptVal("j0", "50");

            bool downloadFail = false;

            try
            {
                HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Add("User-agent", "SecuritySystem (https://github.com/MishaProductions/SecuritySystem)");
                var data = await httpClient.GetAsync("https://api.weather.gov/points/" + Configuration.Instance.WeatherCords);
                if (data.IsSuccessStatusCode)
                {
                    dynamic fs = JObject.Parse(await data.Content.ReadAsStringAsync());

                    string url = "";

                    if (show7Day)
                        url = (string)fs.properties.forecast;
                    else
                        url = (string)fs.properties.forecastHourly;

                    SetCmptText("tLdr", "Downloading Weather data");
                    SetCmptVal("j0", "68");

                    var data2 = await httpClient.GetAsync(url);
                    dynamic fs2 = JObject.Parse(await data2.Content.ReadAsStringAsync());
                    if (data2.IsSuccessStatusCode)
                    {
                        SetCmptText("tLdr", "Processing Weather data");
                        SetCmptVal("j0", "75");
                        var today = fs2.properties.periods[0];
                        SetCmptText("tCap1", (string)today.shortForecast);
                        SetCmptText("tCap2", (string)today.name);
                        SetCmptText("tOverallTemp", ((int)today.temperature).ToString() + "F");



                        /*if (today.temperature != null)
                        {
                            result += $"Temperature: {(int)today.temperature}\r\n";
                        }
                        if (today.shortForecast != null)
                        {
                            result += $"{(string)today.shortForecast}\r\n";
                        }
                        if (today.probabilityOfPrecipitation.value != null)
                        {
                            result += $"Participation chance: {(int)today.probabilityOfPrecipitation.value}%\r\n";
                        }*/

                    }
                    else
                    {
                        downloadFail = true;
                    }
                }
                else
                {
                    downloadFail = true;
                }
            }
            catch (Exception ex)
            {
                SetCmptText("tCap1", "Failed to download weather data:");
                SetCmptText("tCap2", ex.Message);
                SetCmptVisible("tLdr", false);
                SetCmptVisible("j0", false);
                return;
            }

            if (downloadFail)
            {
                SetCmptText("tCap1", "Failed to download weather data");
                SetCmptText("tCap2", "");
                SetCmptVisible("tLdr", false);
                SetCmptVisible("j0", false);
                return;
            }


            SetCmptText("tLdr", "Reading...");
            SetCmptVal("j0", "75");

            //SetCmptText("tCap1", "Snowing In Florida");
            //SetCmptText("tCap2", "High: 90f, Low: 85f");
            //SetCmptText("tOverallTemp", "70f");

            if (show7Day)
            {
                SetCmptText("tViewText", "Week View");

                // assign week labels
                for (int i = 0; i < WeekLabelText.Length; i++)
                {
                    SetCmptText(WeekLabels[i], WeekLabelText[i]);
                }
            }
            else
            {
                SetCmptText("tViewText", "7 Hour View");

                for (int i = 0; i < WeekLabelText.Length; i++)
                {
                    SetCmptText(WeekLabels[i], "+" + (i + 1) + "hr");
                }
            }

            SetCmptVisible("tLdr", false);
            SetCmptVisible("j0", false);
        }
        private void HandleStringCommand(string x)
        {
            if (x == "init")
            {
                currentPage = "pageHome";
                InitKeypad();
            }
            else if (x == "init home")
            {
                InitKeypad(false);
            }
            else if (x == "init settingsui")
            {
                currentPage = "pageSettings";

                if (Configuration.Instance.SystemArmed)
                {
                    SetPage("pageHome");
                    return;
                }

                SendCommand("sw0.val=" + (DoorChime ? "1" : "0"));
            }
            else if (x == "init displaycfg")
            {
                if (Configuration.Instance.SystemArmed)
                {
                    SetPage("pageHome");
                    return;
                }
                currentPage = "pageConfig";

                if (OperatingSystem.IsLinux())
                {
                    double ramUse = CpuMemoryMetrics4LinuxUtils.GetOccupiedMemoryPercentage();
                    double cpuUse = CpuMemoryMetrics4LinuxUtils.GetOverallCpuUsagePercentage();
                    double temp = int.Parse(File.ReadAllText("/sys/class/thermal/thermal_zone0/temp")) / 1000.0;

                    SendCommand("tRamUse.txt=\"RAM usage: " + ramUse + "%\"");
                    SendCommand("tCpuUse.txt=\"CPU usage: " + cpuUse + "%\"");
                    SendCommand("tCpuTemp.txt=\"CPU temp: " + temp + "C\"");
                }
                else
                {
                    SendCommand("tRamUse.txt=\"RAM usage: Unsupported\"");
                    SendCommand("tCpuUse.txt=\"CPU usage: Unsupported\"");
                    SendCommand("tCpuTemp.txt=\"CPU temp: Unsupported\"");
                }

                SendCommand("tBldDate.txt=\"Controller build time: " + new DateTime(Builtin.CompileTime) + "\"");
                SendCommand("tSysUptime.txt=\"System uptime: " + GetReadableTimeSpan(TimeSpan.FromMilliseconds(Environment.TickCount64)) + "\"");

                //Set the IP Address
                bool fail = false;
                try
                {
                    var ip = HttpFrontendServer.GetLocalIPAddress();
                    if (ip != null)
                    {
                        SendCommand("desc0.txt=\"For more settings, please visit http://" + ip + "\"");
                    }
                    else
                    {
                        fail = true;
                    }
                }
                catch
                {
                    fail = true;
                }
                if (fail)
                {
                    SendCommand("desc0.txt=\"Connecting to WI-FI failed.");
                }
            }
            else if (x == "init pageAnnc" || x == "pageAnnc")
            {
                currentPage = "pageAnnc";
                SendCommand("h2.val=" + MusicPlayer.Anncvol);
                int comptIndex = 2;
                foreach (var item in MusicPlayer.AnncFiles)
                {
                    SendCommand($"annc{(comptIndex - 1)}.txt=\"{item}\"");
                    comptIndex++;

                    if (comptIndex > 49)
                    {
                        Console.WriteLine("WARN: not enough buttons for all annc mp3s");
                        break;
                    }
                }
            }
            else if (x == "init musicpage")
            {
                currentPage = "pageMusic";

                //send it
                string musicList = "";
                foreach (var item in MusicPlayer.MusicFiles)
                {
                    musicList += item + "\r\n";
                }

                SetCmptVisible("tLdr", true);

                SendCommand($"select0.path=\"{musicList}\"");

                SendCommand("h1.val=" + MusicPlayer.MusicVol);
                SendCommand("h2.val=" + MusicPlayer.Anncvol);
                SendCommand("sw0.val=" + (ShufflePlaylist ? "1" : "0"));
                SendCommand("sw1.val=" + (LoopPlaylist ? "1" : "0"));

                if (MusicPlayer.PlaylistMode)
                {
                    SendCommand("vis bPlaylistBack,1");
                    SendCommand("vis bPlaylistNext,1");
                }
                else
                {
                    SendCommand("vis bPlaylistBack,0");
                    SendCommand("vis bPlaylistNext,0");
                }

                if (MusicPlayer.MusicPlaying)
                {
                    SendCommand("tCurrentSong.txt=\"" + MusicPlayer.CurrentSongName + "\"");
                }

                SetCmptVisible("tLdr", false);
            }
            else if (x == "init WeatherPage")
            {
                currentPage = "pageWeather";
                SendCommand("delfile \"sd0/test2.tft\"");
                InitWeatherPage(false);
            }
            else if (x == "weather setview 7hr")
            {
                currentPage = "pageWeather";
                InitWeatherPage(false);
            }
            else if (x == "weather setview 7d")
            {
                currentPage = "pageWeather";
                InitWeatherPage(true);
            }
            else if (x == "playall")
            {
                if (MusicPlayer.MusicPlaying)
                    return;
                MusicPlayer.PlayAllMusic(MusicPlayer.MusicFiles);

                SendCommand($"tCurrentSong.txt=\"{MusicPlayer.CurrentSongName}\"");

                SendCommand("vis bPlaylistBack,1");
                SendCommand("vis bPlaylistNext,1");
            }
            else if (x == "stopmusic")
            {
                if (!MusicPlayer.MusicPlaying)
                    return;
                MusicPlayer.StopMusic();

                SendCommand("vis bPlaylistBack,0");
                SendCommand("vis bPlaylistNext,0");
                SendCommand($"tCurrentSong.txt=\"Current file: none\"");
            }
            else if (x.StartsWith("playmusicid "))
            {
                int a = int.Parse(x.Replace("playmusicid ", ""));
                MusicPlayer.PlayMusic(a);

                SendCommand("tCurrentSong.txt=\"" + MusicPlayer.CurrentSongName + "\"");
                SendCommand("vis bPlaylistBack,0");
                SendCommand("vis bPlaylistNext,0");
            }
            else if (x == "stopannc")
            {
                if (!MusicPlayer.AnncPlaying)
                    return;
                MusicPlayer.StopAnnc();
                //SendCommand("vis bPlayAnnc,1");
            }
            else if (x == "ping controller")
            {
                lastDisplayPing = DateTime.UtcNow;
                SendCommand("click bPingResponse,0");
                SendCommand("click bPingResponse,1");
            }
            else if (x == "playlist next")
            {
                if (MusicPlayer.PlaylistMode && MusicPlayer.MusicPlaying)
                {
                    MusicPlayer.PlaylistForward();

                    SendCommand("select0.val=" + MusicPlayer.musicProc.PlaylistIndex);
                    SendCommand("tCurrentSong.txt=\"" + MusicPlayer.MusicFiles[(int)MusicPlayer.musicProc.PlaylistIndex] + "\"");
                }
            }
            else if (x == "playlist back")
            {
                if (MusicPlayer.PlaylistMode && MusicPlayer.MusicPlaying && MusicPlayer.musicProc.PlaylistIndex != 0)
                {
                    MusicPlayer.PlaylistBack();

                    SendCommand("select0.val=" + MusicPlayer.musicProc.PlaylistIndex);
                    SendCommand("tCurrentSong.txt=\"" + MusicPlayer.CurrentSongName + "\"");
                }
            }
            else if (x.StartsWith("setsvol "))
            {
                int a = int.Parse(x.Replace("setsvol ", ""));
                MusicPlayer.MusicVol = a;
            }

            else if (x.StartsWith("setanncvol "))
            {
                int a = int.Parse(x.Replace("setanncvol ", ""));
                MusicPlayer.Anncvol = a;
            }
            else if (Configuration.CheckIfCodeCorrect(x))
            {
                //hide the hud
                HideHud();

                if (Configuration.Instance.SystemArmed)
                {
                    Console.WriteLine("keypad: Disarming system");
                    SystemManager.DisarmSystem();
                }
                else
                {
                    Console.WriteLine("keypad: Arming system");
                    if (ZoneController.IsReady)
                    {
                        SystemManager.ArmSystem();
                    }
                    else
                    {
                        Console.WriteLine("keypad: cannot arm system as zones are not ready");
                    }
                    UpdateStatusText();
                }
                UpdateStatusText();
            }
            else if (x.StartsWith("wifipwverify "))
            {
                var str = x.Replace("wifipwverify ", "");
                if (Configuration.CheckIfCodeCorrect(str))
                {
                    WlanAuthenticated = true;
                    SetPage("pageWlan");
                }
                else
                {
                    SendCommand("vis tIncorrectPW,1");
                }
            }
            else if (x == "wifi deauth")
            {
                WlanAuthenticated = false;
            }
            else if (x == "doorchime enable")
            {
                DoorChime = true;
            }
            else if (x == "doorchime disable")
            {
                DoorChime = false;
            }
            else if (x == "setshuffle 0")
            {
                ShufflePlaylist = false;
                MusicPlayer.SetMusicShuffle(false);
            }
            else if (x == "setshuffle 1")
            {
                ShufflePlaylist = true;
                MusicPlayer.SetMusicShuffle(true);
            }
            else if (x == "setloop 0")
            {
                LoopPlaylist = false;
                MusicPlayer.SetMusicLoop(false);
            }
            else if (x == "setloop 1")
            {
                LoopPlaylist = true;
                MusicPlayer.SetMusicLoop(true);
            }
            else if (x == "sysmsg init")
            {
                PromptOpen = true;
                currentPage = "pageSysMsg";
            }
            else if (x == "sysmsg 1")
            {
                if (PromptOpen && PromptB1Action != null)
                {
                    PromptB1Action();
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x == "sysmsg 2")
            {
                if (PromptOpen && PromptB2Action != null)
                {
                    PromptB2Action();
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x == "sysmsg 3")
            {
                if (PromptOpen && PromptB3Action != null)
                {
                    PromptB3Action();
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x.StartsWith("dispversion "))
            {
                int a = int.Parse(x.Replace("dispversion ", ""));

                if (a != 201 && !ShowedFirmwareMismatch)
                {
                    ShowPrompt("Firmware Mismatch", "The display firmware version does not\r\nmatch with the controller firmware\r\nversion. Problems may occur. ", true, 9,
                    "OK", true, () =>
                    {
                        PromptOpen = false;
                        ShowedFirmwareMismatch = true;
                        SetPage("pageHome");
                    },
                    "", false, null,
                    "Reload", true, () =>
                    {
                        SetPage("pagePreboot");
                    }, 49152);
                }
            }
            else if (x == "prompt weatherConfirm")
            {
                SetDispPage("pageWeather");
            }
            else if (x == "prompt showTrouble")
            {
                HandleShowTrouble();
            }
            else if (x == "doSystemRestart")
            {
                if (!Configuration.Instance.SystemArmed)
                {
                    ShowPrompt("Warning", "System will not be monitoring during\r\nthe restart. Continue?", true, 9,
                    "Yes", true, () =>
                    {
                        SystemManager.WriteToEventLog("System restart from Nextion Display");
                        PromptOpen = false;
                        SetPage("pageBoot");
                        SendCommand("play 0,5,1");
                        Thread.Sleep(1000);
                        ShowPrompt("", "Restarting controller...", false, 0, "", false, null, "", false, null, "", false, null);
                        Configuration.Save();
                        Process.Start("/sbin/reboot").WaitForExit();
                    },
                    "", false, null,
                    "No", true, () =>
                    {
                        PromptOpen = false;
                        SetPage("pageSettings");
                    }, 49152);
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x == "doServiceRestart")
            {
                if (!Configuration.Instance.SystemArmed)
                {
                    ShowPrompt("Warning", "System will not be monitoring while service restart in progres.\nContinue?", true, 9,
                    "Yes", true, () =>
                    {
                        PromptOpen = false;
                        SendCommand("play 0,5,0");
                        ShowPrompt("", "Restarting service...", false, 0, "", false, null, "", false, null, "", false, null);
                        SystemManager.WriteToEventLog("Service restart from Nextion Display");
                        Configuration.Save();
                        Thread.Sleep(1000);

                        Process.Start("/usr/bin/systemctl", "restart secsys").WaitForExit();
                    },
                    "", false, null,
                    "No", true, () =>
                    {
                        PromptOpen = false;
                        SetPage("pageSettings");
                    }, 49152);
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x == "doSystemPowerOff")
            {
                if (!Configuration.Instance.SystemArmed)
                {
                    ShowPrompt("Warning", "System will not be monitoring. Continue?", true, 9,
                    "Yes", true, () =>
                    {
                        PromptOpen = false;
                        SendCommand("play 0,5,0");
                        ShowPrompt("", "Shutting down...", false, 0, "", false, null, "", false, null, "", false, null);
                        SystemManager.WriteToEventLog("System shutdown from Nextion Display");
                        Configuration.Save();
                        Thread.Sleep(1000);

                        Process.Start("/sbin/poweroff").WaitForExit();
                    },
                    "", false, null,
                    "No", true, () =>
                    {
                        PromptOpen = false;
                        SetPage("pageSettings");
                    }, 49152);
                }
                else
                {
                    ShowNotImpl(x);
                }
            }
            else if (x.StartsWith("syssetup "))
            {
                HandleSystemSetup(x.Replace("syssetup ", ""));
            }
            else
            {

                SendCommand("play 0,4,0"); //channel 1, play resource 2, no loop
                Console.WriteLine(x);

                //wrong password
                //SendCommand($"click bCancel,1");
                //SendCommand($"click bCancel,1");
                ShowSimpleWarning("Error", "Network username or password is incorrect", () =>
            {
                PromptOpen = false;
                InitKeypad();
            });
            }
        }

        // https://stackoverflow.com/a/34391569/11250752
        public string GetReadableTimeSpan(TimeSpan value)
        {
            string duration = "";

            var totalDays = (int)value.TotalDays;
            if (totalDays >= 1)
            {
                duration = totalDays + " day" + (totalDays > 1 ? "s" : string.Empty);
                value = value.Add(TimeSpan.FromDays(-1 * totalDays));
            }

            var totalHours = (int)value.TotalHours;
            if (totalHours >= 1)
            {
                if (totalDays >= 1)
                {
                    duration += ", ";
                }
                duration += totalHours + " hour" + (totalHours > 1 ? "s" : string.Empty);
                value = value.Add(TimeSpan.FromHours(-1 * totalHours));
            }

            var totalMinutes = (int)value.TotalMinutes;
            if (totalMinutes >= 1)
            {
                if (totalHours >= 1)
                {
                    duration += ", ";
                }
                duration += totalMinutes + " minute" + (totalMinutes > 1 ? "s" : string.Empty);
            }

            return duration;
        }

        private bool SysSetupAuthenticated = false;
        private string SysSetupUser = "";

        private void HandleSystemSetup(string cmd)
        {
            if (cmd.StartsWith("login "))
            {
                SysSetupAuthenticated = false;
                SysSetupUser = "";

                string[] data = cmd.Replace("login ", "").Split(";");
                if (data.Length != 2)
                {
                    ShowSimpleWarning("Error", "Communications error", () =>
                    {
                        PromptOpen = false;
                        InitKeypad();
                    });
                    return;
                }

                string username = data[0];
                string password = data[1];

                if (Configuration.Instance.SystemArmed)
                {
                    SetCmptText("tError", "System is currently armed.");
                    return;
                }

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    SetCmptText("tError", "Username/Pass is empty");
                    return;
                }

                foreach (var item in Configuration.Instance.Users)
                {
                    if (item.Username == username && item.PasswordHash.Equals(SecurityApiController.Sha256(password), StringComparison.CurrentCultureIgnoreCase))
                    {
                        SysSetupAuthenticated = true;
                        SysSetupUser = item.Username;
                        break;
                    }
                }

                if (!SysSetupAuthenticated)
                {
                    SetCmptText("tError", "No such user or password");
                    return;
                }

                SetPage("sysSetupMain");
            }
            else if (cmd == "logout")
            {
                SysSetupAuthenticated = false;
                SysSetupUser = "";
            }

            if (!SysSetupAuthenticated)
            {
                return;
            }

            // Notification configuration
            if (cmd == "notifSetup loadCfg")
            {
                SetCmptVal("sw0", Configuration.Instance.SmtpEnabled);
                SetCmptVal("cb0", Configuration.Instance.NotificationLevel);
                SetCmptText("t2", Configuration.Instance.SmtpHost);
                SetCmptText("t3", Configuration.Instance.SmtpUsername); //t5: password
                SetCmptText("t6", Configuration.Instance.SmtpSendTo);
            }
            else if (cmd == "notifSetup testEmail")
            {
                SetCmptText("tError", "Please wait");
                try
                {
                    MailClass.SendMailNonThreaded($"If this was not you, take steps. Test email sent from security system.<br><small>Generated on {DateTime.Now}</small>", $"User {SysSetupUser} on Display 0 sent a test email", false);
                }
                catch (Exception ex)
                {
                    SetCmptText("tError", ex.Message);
                    return;
                }
                SetCmptText("tError", "Success");
            }
            else if (cmd == "notifSetup saveCfg")
            {
            ShowSimpleWarning("Error", "Function not implemented", () =>
            {
                PromptOpen = false;
                SetPage("sysSetupNotif");
            });
            }
        }
        public void HideHud()
        {
            SendCommand("click fHideKeypad,0");
            SendCommand("click fHideKeypad,1");
        }
        public void ButtonBeep()
        {
            if (!KeypadSendCommands)
                return;
            SendCommand("play 1,0,0"); //channel 1, play resource 0, no loop
        }
        public void AlarmBeep()
        {
            if (!KeypadSendCommands)
                return;
            SendCommand("play 1,1,0"); //channel 1, play resource 1, no loop
        }

        public void ErrorBeep()
        {
            SendCommand("play 1,2,0"); //channel 1, play resource 2, no loop
        }

        public void TroubleBeep()
        {
            SendCommand("play 1,4,0"); //channel 1, play resource 2, no loop
        }

        private void SetPage(string pg)
        {
            currentPage = pg;
            SendCommand("page " + pg);
        }
    }
    [Flags] // a zone can have trouble and be ready at same time
    public enum ZoneState
    {
        Unconfigured,
        Ready,
        NotReady,
        Trouble = 4
    }
}
