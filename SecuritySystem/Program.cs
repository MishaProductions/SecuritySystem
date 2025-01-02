using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.WebApi;
using MHSApi.API;
using SecuritySystem.Modules;
using SecuritySystem.Modules.NXDisplay;
using SecuritySystem.Utils;
using System.Device.Gpio;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SecuritySystem
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("┌────────────────────────────────┐");
            Console.WriteLine("│                                │");
            Console.WriteLine("│ MHS Security System Controller │");
            Console.WriteLine("│                                │");
            Console.WriteLine("│ Build date: " + new DateTime(Builtin.CompileTime).ToString("MM/dd/yyyy HH:mm:ss") + "│");
            Console.WriteLine("└────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Loading configuration");
            Configuration.Save();

            if (!Configuration.Instance.SystemSetUp)
            {
                Console.WriteLine("System is not setup or configuration is invalid. Starting setup webserver...");

                HttpFrontendServer.Start();

                while(!Configuration.Instance.SystemSetUp)
                {
                   Thread.Sleep(1000);
                }
            }
            // TODO FIX

            //Console.WriteLine("NetworkManager...");
            //NetworkManager.Initialize();

            Console.WriteLine("Probing zones (this might take some time)...");
            ZoneController.Initialize();

            Console.WriteLine("Loading SMTP module");
            ModuleController.RegisterModule(new MailClass());

            Console.WriteLine("Initializing devices (this might take some time)...");
            DeviceSubsys.DeviceModel.InitializeAll();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ModuleController.RegisterModule(new NextionDisplay("/dev/ttyS3"));
            }

            Console.WriteLine("Starting webserver");
            HttpFrontendServer.Start();

            Console.WriteLine("Starting system timer");
            Thread t2 = new(TimerThread);
            t2.Start();

            Console.WriteLine("Restoring previous state");
            if (Configuration.Instance.SystemAlarmState)
            {
                //SystemManager.StartAlarm(Configuration.Instance.AlarmZone);

                Console.WriteLine("Detected alarm on zone #" + Configuration.Instance.AlarmZone);
            }

            Console.WriteLine("Starting zone monitoring");
            ZoneController.Start();
        }
        private static async void TimerThread()
        {
            while (true)
            {
                // 1. wait for the system to be armed by SystemManager.ArmSystem
                // 2. Exit delay
                // 3. Monitor for zone changes. If zone changed start entry delay
                // 4. Once entry delay expires broadcast alarm.


                Console.WriteLine("timer: Waiting for system to be armed...");

                // Wait until the system is not armed and the alarm state to be cleared
                while (!Configuration.Instance.SystemArmed || Configuration.Instance.SystemAlarmState)
                {
                    // using Task.Delay avoids random lockups. Thanks .NET!
                    await Task.Delay(20);
                }

                //The system got armed so we are now in the exit delay
                Console.WriteLine("timer: Detected that system is armed. As a result, doing exit delay.alarm=" + Configuration.Instance.SystemAlarmState);
                while (true)
                {
                    Configuration.Instance.Timer--;
                    SystemManager.SendSysTimer(false, Configuration.Instance.Timer);
                    if (!Configuration.Instance.SystemArmed)
                    {
                        Console.WriteLine("[timer] detected that the system is disarmed");
                        goto SystemDisarmed;
                    }
                    await Task.Delay(1000);
                    if (!Configuration.Instance.SystemArmed)
                    {
                        Console.WriteLine("[timer] detected that the system is disarmed");
                        goto SystemDisarmed;
                    }

                    if (Configuration.Instance.Timer > 0) continue;
                    Configuration.Instance.InExitDelay = false;
                    break;
                }
                Console.WriteLine("timer: Exit delay over - system is fully on");

                //Wait for a zone to go to the HIGH state, or exit if system is disarmed
                Configuration.Instance.IsZoneOpenedWhenSystemArmed = false;

                Configuration.Instance.SystemArmed = true;
                Configuration.Save();
                while (true)
                {
                    if (Configuration.Instance.IsZoneOpenedWhenSystemArmed)
                    {
                        break;
                    }
                    if (!Configuration.Instance.SystemArmed)
                    {
                        //system is disarmed, exit
                        Console.WriteLine("[timer] detect that the system is disarmed");
                        Configuration.Instance.InEntryDelay = false;
                        goto SystemDisarmed;
                    }

                    //we need to do this dumb thing to bypass some bug
                    await Task.Delay(100);
                }

                //zone is at HIGH state, and system is not disarmed

                Configuration.Instance.Timer = 15;
                Configuration.Instance.InExitDelay = false;
                Configuration.Instance.InEntryDelay = true;
                Console.WriteLine("[timer] Begin Entry Delay");
                Configuration.Save();
                while (true)
                {
                    Console.WriteLine(Configuration.Instance.Timer);
                    Configuration.Instance.Timer--;
                    SystemManager.SendSysTimer(true, Configuration.Instance.Timer);
                    if (!Configuration.Instance.SystemArmed)
                    {
                        Console.WriteLine("[timer] detected that the system is disarmed");
                        Configuration.Instance.InEntryDelay = false;
                        goto SystemDisarmed;
                    }
                    Thread.Sleep(1000);
                    if (Configuration.Instance.Timer <= 0)
                    {
                        Console.WriteLine("[timer] the entry delay has expired. alarm");
                        // Start alarm

                        Configuration.Instance.SystemAlarmState = true;
                        Configuration.Save();

                        SystemManager.StartAlarm(Configuration.Instance.AlarmZone);

                        // Wait for something to disarm it

                        Console.WriteLine("timer: waiting for system disarm");
                        //we now need to wait for the code
                        while (Configuration.Instance.SystemAlarmState)
                        {
                            await Task.Delay(20);
                        }
                        Console.WriteLine("timer: end wait for system disarm");

                        break;
                    }
                    if (!Configuration.Instance.SystemArmed)
                    {
                        Console.WriteLine("[timer] detected that the system is disarmed");
                        Configuration.Instance.AlarmZone = -1;
                        Configuration.Instance.InEntryDelay = false;
                        goto SystemDisarmed;
                    }
                }

            SystemDisarmed:
                Console.WriteLine("[timer] systemdisarmed label");
                Configuration.Instance.SystemArmed = false;
                Configuration.Instance.AlarmZone = -1;
                SystemManager.InvokeSystemDisarm();
                Configuration.Instance.SystemAlarmState = false;
                Configuration.Instance.InExitDelay = false;
                Configuration.Instance.InEntryDelay = false;
                Configuration.Instance.IsZoneOpenedWhenSystemArmed = false;
                Configuration.Save();
            }
        }
    }
}