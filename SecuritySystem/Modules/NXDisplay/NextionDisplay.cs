using MHSApi.API;
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
    public class NextionDisplay : Module
    {
        private Thread? packetReader;
        private bool KeypadSendCommands = true;
        private string? comport;
        public static bool DoorChime = true;
        public bool ShufflePlaylist = false;
        private SerialPort? _port;
        public int UpdateProgress = 0;
        public bool UpdateFail = false;
        public bool UpdateFinish = false;
        public bool UpdateInProgress = false;
        public string UpdateProgressString = "No firmware update in progress";
        public bool WlanAuthenticated = false;
        public bool IsSleepMode = false;
        string currentPage = "pageHome";
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

        public NextionDisplay(string comport)
        {
            this.comport = comport;
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

        private void Init()
        {
            SerialPort = new SerialPort(comport, 9600);
            SerialPort.Open();
            packetReader = new(PacketRxThread);
            packetReader.Start();
            InitKeypad();

            var aTimer = new System.Timers.Timer(1000 * 60 * 30); // update every 30 minutes (in milliseconds)
            aTimer.Elapsed += new ElapsedEventHandler(HandleWeatherTimer);
            aTimer.Start();
        }
        #endregion
        #region Event handlers

        private void HandleWeatherTimer(object? sender, ElapsedEventArgs e)
        {
            RefreshWeather();
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
                SendCommand("vis bPlaySelMusic,1");
                SendCommand("vis bPlayAllMusic,1");
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
                        if (ZoneController.ZoneStates[item.Key] == PinValue.High)
                        {
                            SetZoneStatus(item.Value.ZoneNumber + 1, item.Value.Name, ZoneState.NotReady);
                        }
                        else
                        {
                            SetZoneStatus(item.Value.ZoneNumber + 1, item.Value.Name, ZoneState.Ready);
                        }
                    }
                }
            }

            UpdateStatusText();
        }
        private void SystemManager_OnAlarm(int alarmZone)
        {
            // todo: play some annoying sound
            UpdateStatusText();
        }
        private void SystemManager_OnSystemDisarm(object? sender, EventArgs e)
        {
            // show main view
            UpdateStatusText();
        }
        private void SystemManager_OnSysTimerEvent(bool entry, int timer)
        {
            AlarmBeep();
            UpdateStatusText();
        }
        #endregion
        #region Firmware update
        private uint ReadFileSize(byte[] firmware)
        {
            BinaryReader br = new(new MemoryStream(firmware));
            br.BaseStream.Position = 0x3C;
            return br.ReadUInt32();
        }
        private byte[]? FirmwareData;
        public void UpdateFirmware(byte[] firmware)
        {
            //disable sending of commands and disable packetreader thread
            KeypadSendCommands = false;
            SerialPort?.Close();
            SerialPort = null;

            FirmwareData = firmware;

            UpdateProgressString = "Initializing...";
            DeviceModel.BroadcastFwUpdateProgress("Generic Nextion Display", UpdateProgressString, 0);

            Console.WriteLine("Updating nextion display at " + comport);
            if (comport == null)
            {
                throw new Exception("comport cannot be NULL");
            }
            //wait for other thread to die
            Thread.Sleep(2000);
            Thread worker = new(FirmwareUpdateThread);
            worker.Start();
        }
        private void FirmwareUpdateThread()
        {
            if (FirmwareData == null) throw new Exception("firmware to upload cannot be null");

            // The code here is based on https://github.com/MMMZZZZ/Nexus/blob/master/Nexus.py
            SerialPort = new SerialPort(comport, 9600)
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
            SendCommand($"whmi-wris {fwsize},9600,1");

            SerialPort.Close();
            SerialPort = new SerialPort(comport, 9600)
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
            DateTime starttime = DateTime.Now;
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

            Console.WriteLine("NextionDisplay: initializing");

            SetCmptVal("pageBoot.j0", "100");
            SendCommand("bkcmd=3");

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
            SendCommand($"vis pStatus,0");
            RefreshWeather();
            SystemManager_OnZoneUpdate(false, 0, "", ZoneState.Unconfigured);
        }

        public async void RefreshWeather()
        {
            if (IsSleepMode) return;
            if (currentPage != "pageHome" || PromptOpen)
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
                if (ZoneController.ZoneStates[item.Key] == PinValue.Low)
                {

                }
                else
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
                SendCommand($"vis bMusicLoop,0");
                SendCommand($"vis b0,0");
            }
            else
            {
                SendCommand($"vis bSettings,1");
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

            //set color
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
            UpdateStatusText();
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
                            Console.WriteLine("Got text: " + x);

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

        private readonly string[] WeatherLabels = ["t19", "t20", "t21", "t22", "t23", "t24", "t25"];
        private readonly string[] TempLabels = ["t12", "t13", "t14", "t15", "t16", "t17", "t18"];
        private readonly string[] WeekLabels = ["t5", "t6", "t7", "t8", "t9", "t10", "t11"];
        private readonly string[] WeekLabelText = ["Saturday", "Monday", "Tuesday", "Wenesday", "Thursday", "Friday", "Sat"];
        private bool PromptOpen = false;

        private Action? PromptB1Action;
        private Action? PromptB2Action;
        private Action? PromptB3Action;
        private bool ShowedFirmwareMismatch = false;

        private void ShowPrompt(string title, string message, bool iconVisible, int icon, string b1Text, bool b1Visible, Action? b1Click, string b2Text, bool b2Visible, Action? b2Click, string b3Text, bool b3Visible, Action? b3Click, int bg = 16904)
        {
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
                InitKeypad();
            });
        }
        private void ShowBadOperation(string detail)
        {
            ShowSimpleWarning("Communication Fail", "The requested action is not currently\r\navailable. Press OK to reload. Details:\r\n" + detail, () =>
            {
                InitKeypad();
            });
        }
        private void InitWeatherPage(bool show7Day)
        {
            if (currentPage != "pageWeather")
            {
                Console.WriteLine("nextion: attempted to init weather page when not weather page!");
                return;
            }

            SetCmptVisible("j0", true);
            SetCmptVisible("tLdr", true);
            SetCmptText("tLdr", "Downloading Weather Data");
            SetCmptVal("j0", "50");


            SetCmptText("tLdr", "Reading...");
            SetCmptVal("j0", "75");

            SetCmptText("tCap1", "Snowing In Florida");
            SetCmptText("tCap2", "High: 90f, Low: 85f");
            SetCmptText("tOverallTemp", "70f");

            if (show7Day)
            {
                SetCmptText("tViewText", "Week View");

                // assign week labels
                for (int i = 0; i > WeekLabelText.Length; i++)
                {
                    SetCmptText(WeekLabels[i], WeekLabelText[i]);
                }
            }
            else
            {
                SetCmptText("tViewText", "7 Hour View");

                for (int i = 0; i > WeekLabelText.Length; i++)
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


                SendCommand("tBldDate.txt=\"Controller build time: " + new DateTime(Builtin.CompileTime) + "\"");
                SendCommand("tSysUptime.txt=\"System uptime: " + TimeSpan.FromMilliseconds(Environment.TickCount64) + "\"");

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


                SendCommand("sw0.val=" + (DoorChime ? "1" : "0"));
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

                SendCommand($"select0.path=\"{musicList}\"");

                if (MusicPlayer.AnncPlaying)
                {
                    SendCommand("vis bPlayAnnc,0");
                }
                if (MusicPlayer.MusicPlaying)
                {
                    SendCommand("vis bPlaySelMusic,0");
                    SendCommand("vis bPlayAllMusic,0");
                }

                SendCommand("h1.val=" + MusicPlayer.MusicVol);
                SendCommand("h2.val=" + MusicPlayer.Anncvol);
                SendCommand("sw0.val=" + (ShufflePlaylist ? "1" : "0"));

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
            }
            else if (x == "init WeatherPage")
            {
                currentPage = "pageWeather";
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
                SendCommand("vis bPlaySelMusic,0");
                SendCommand("vis bPlayAllMusic,0");

                SendCommand("vis bPlaylistBack,1");
                SendCommand("vis bPlaylistNext,1");
            }
            else if (x == "stopmusic")
            {
                if (!MusicPlayer.MusicPlaying)
                    return;
                MusicPlayer.StopMusic();

                SendCommand("vis bPlaySelMusic,1");
                SendCommand("vis bPlayAllMusic,1");

                SendCommand("vis bPlaylistBack,0");
                SendCommand("vis bPlaylistNext,0");
                SendCommand($"tCurrentSong.txt=\"Current file: none\"");
            }
            else if (x.StartsWith("playmusicid "))
            {
                int a = int.Parse(x.Replace("playmusicid ", ""));
                MusicPlayer.PlayMusic(a);

                SendCommand("tCurrentSong.txt=\"" + MusicPlayer.CurrentSongName + "\"");

                SendCommand("vis bPlaySelMusic,0");
                SendCommand("vis bPlayAllMusic,0");

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
                    bool ready = true;
                    //We need to verify that all zones are indeed ready (never trust the client!)
                    foreach (var item in Configuration.Instance.Zones)
                    {
                        if (ZoneController.ZoneStates[item.Key] == PinValue.Low)
                        {

                        }
                        else
                        {
                            if (item.Value.Type != ZoneType.None)
                            {
                                //umm
                                ready = false;
                            }
                        }
                    }
                    if (ready)
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
            }
            else if (x == "setshuffle 1")
            {
                ShufflePlaylist = true;
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
            else if (x == "doSystemPowerOff")
            {
                if (!Configuration.Instance.SystemArmed)
                {
                    ShowPrompt("Warning", "System will not be monitoring. Continue?", true, 9,
                    "Yes", true, () =>
                    {
                        PromptOpen = false;
                        SystemManager.WriteToEventLog("System shutdown from Nextion Display");
                        Configuration.Save();
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
            else
            {
                Console.WriteLine(x);
                //wrong password
                //SendCommand($"click bCancel,1");
                //SendCommand($"click bCancel,1");
                ShowNotImpl(x);
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
            SendCommand("play 0,1,0"); //channel 1, play resource 1, no loop
        }

        private void SetPage(string pg)
        {
            currentPage = pg;
            SendCommand("page " + pg);
        }
    }
    public enum ZoneState
    {
        Unconfigured,
        Ready,
        NotReady,
    }
}
