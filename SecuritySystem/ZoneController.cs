using MHSApi.API;
using SecuritySystem.Modules.NXDisplay;
using SecuritySystem.Utils;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Runtime.InteropServices;

namespace SecuritySystem
{
    public static class ZoneController
    {
        private static GpioController? controller;
        public static PinValue[] ZoneStates = new PinValue[20];

        public static bool IsReady
        {
            get
            {
                if (Configuration.Instance.Zones.Count == 0) return true;
                foreach (var item in Configuration.Instance.Zones)
                {
                    if (ZoneStates[item.Key] == PinValue.Low)
                    {

                    }
                    else
                    {
                        if (item.Value.Type != ZoneType.None)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public static void Initialize()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("ZoneController: skipped initalization as not running on linux");
                return;
            }

            controller = Configuration.Instance.UseOrangePiDriver ? new GpioController(PinNumberingScheme.Logical, new OrangePiPCDriver()) : new GpioController(PinNumberingScheme.Logical);

            ZoneStates = new PinValue[Configuration.Instance.Zones.Count];
            foreach (var item in Configuration.Instance.Zones)
            {
                var zoneIdx = item.Key;
                controller.OpenPin(item.Value.GpioPin);
                controller.SetPinMode(item.Value.GpioPin, PinMode.Input);
                var state = controller.Read(item.Value.GpioPin);
                ZoneStates[zoneIdx] = state;
                Console.WriteLine(" - Pin #" + item.Value.GpioPin + " is " + state);
            }
        }

        public static void Start()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("ZoneController: skipped start as not running on linux");

                while (true)
                {

                    // Allow for debugging on windows
                    string? s = Console.ReadLine();
                    if (s == "a")
                    {
                        ZoneStates[0] = PinValue.High;
                        SystemManager.SendZoneUpdateSingleToAll(true, 0, "Zoning", ZoneState.NotReady);
                        Console.WriteLine("set zone 1 to not ready");

                        // copy and pasted
                        Configuration.Instance.IsZoneOpenedWhenSystemArmed = true;
                        if (Configuration.Instance.SystemArmed)
                        {
                            Configuration.Instance.AlarmZone = 1;
                        }

                        Console.WriteLine($"Zone #1 is not ready.");
                        if (Configuration.Instance.SystemArmed && !Configuration.Instance.InExitDelay)
                        {
                            Console.WriteLine($"[zone monitor]: Begin entry delay. alarm zone is " + Configuration.Instance.AlarmZone + "," + Configuration.Instance.IsZoneOpenedWhenSystemArmed);

                            //Closed
                            SystemManager.SendSysTimer(true, Configuration.Instance.Timer);
                        }
                    }
                    else if (s == "b")
                    {
                        ZoneStates[0] = PinValue.Low;
                        SystemManager.SendZoneUpdateSingleToAll(true, 0, "Zoning", ZoneState.Ready);
                        Console.WriteLine("set zone 1 to ready");
                    }
                }
            }
            if (controller == null)
            {
                throw new Exception("ZoneController::Initialize not called");
            }

            while (true)
            {
                foreach (var zoneEntry in Configuration.Instance.Zones)
                {
                    var zone = zoneEntry.Value;
                    var zonePin = zone.GpioPin;
                    var zoneValue = controller.Read(zonePin);
                    var zoneIdx = zone.ZoneNumber;
                    var zoneUserValue = zoneIdx + 1;

                    if (ZoneStates[zoneIdx] != zoneValue)
                    {
                        //zone state changed
                        ZoneStates[zoneIdx] = zoneValue;

                        if (zone.Type != ZoneType.None)
                        {
                            SystemManager.SendZoneUpdateSingleToAll(true, zoneUserValue, zone.Name, zoneValue == PinValue.High ? ZoneState.NotReady : ZoneState.Ready);

                            if (zoneValue == PinValue.High)
                            {
                                //zone is open
                                Configuration.Instance.IsZoneOpenedWhenSystemArmed = true;
                                if (Configuration.Instance.SystemArmed)
                                {
                                    Configuration.Instance.AlarmZone = zoneUserValue;
                                }
                                Console.WriteLine($"Zone #{zoneUserValue} is not ready.");
                                if (Configuration.Instance.SystemArmed && !Configuration.Instance.InExitDelay)
                                {
                                    Console.WriteLine($"[zone monitor]: Begin entry delay. alarm zone is " + Configuration.Instance.AlarmZone + "," + Configuration.Instance.IsZoneOpenedWhenSystemArmed);

                                    //Closed
                                    SystemManager.SendSysTimer(true, Configuration.Instance.Timer);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Zone #{zoneUserValue} is now ready.");
                            }
                        }
                    }
                }
                Thread.Sleep(300);
            }
        }
    }
}