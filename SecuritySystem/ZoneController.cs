using MHSApi.API;
using SecuritySystem.Modules;
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
        public static ZoneState[] ZoneStates = new ZoneState[20];

        public static bool IsReady
        {
            get
            {
                if (Configuration.Instance.Zones.Count == 0) return true;
                foreach (var item in Configuration.Instance.Zones)
                {
                    if (ZoneStates[item.Key] == ZoneState.NotReady && item.Value.Type != ZoneType.None) return false;
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

            controller = Configuration.Instance.UseOrangePiDriver ? new GpioController(new OrangePiPCDriver()) : new GpioController(new LibGpiodDriver(gpioChip: 4));

            ZoneStates = new ZoneState[Configuration.Instance.Zones.Count];
            foreach (var item in Configuration.Instance.Zones)
            {
                var zoneIdx = item.Key;
                controller.OpenPin(item.Value.GpioPin);
                controller.SetPinMode(item.Value.GpioPin, PinMode.Input);
                var state = controller.Read(item.Value.GpioPin);
                ZoneStates[zoneIdx] = PinValueToZoneState(state);
                Console.WriteLine(" - Pin #" + item.Value.GpioPin + " is " + state);
            }

            SystemManager.OnInactiveZone += SystemManager_OnInactiveZone;
            SystemManager.OnInactiveZoneIgnore += SystemManager_OnInactiveZoneIgnore;
        }

        private static ZoneState PinValueToZoneState(PinValue val)
        {
            return val == PinValue.High ? ZoneState.NotReady : ZoneState.Ready;
        }

        public static bool IsReadySet(ZoneState state)
        {
            return (state & ZoneState.Ready) != 0;
        }
        public static bool IsZoneReady(int idx)
        {
            return IsReadySet(ZoneStates[idx]);
        }
        public static bool IsTroubleSet(ZoneState state)
        {
            return (state & ZoneState.Trouble) != 0;
        }

        public static ZoneState Simplify(ZoneState state)
        {
            // remove trouble flag
            return state & ~ZoneState.Trouble;
        }

        private static void SystemManager_OnInactiveZone(int index, TimeSpan lastReadyDur)
        {
            ZoneStates[index] |= ZoneState.Trouble;
            SystemManager.SendZoneUpdateSingleToAll(true, index + 1, Configuration.Instance.Zones[index].Name, ZoneStates[index]);
        }
        private static void SystemManager_OnInactiveZoneIgnore(int index)
        {
            ZoneStates[index] |= ~ZoneState.Trouble;
            SystemManager.SendZoneUpdateSingleToAll(true, index + 1, Configuration.Instance.Zones[index].Name, ZoneStates[index]);
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
                        ZoneStates[0] = ZoneState.NotReady;
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
                        ZoneStates[0] = ZoneState.Ready;
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
                    var oldValue = ZoneStates[zoneIdx];

                    var newZoneState = PinValueToZoneState(zoneValue);

                    if (Simplify(ZoneStates[zoneIdx]) != newZoneState)
                    {
                        // check if the zone was "inactive" previously and now ready, if so remove trouble flag. Add it back if nessesary
                        if (newZoneState == ZoneState.Ready && InactiveZoneMonitor.IsInactive(zoneIdx))
                            ZoneStates[zoneIdx] &= ~ZoneState.Trouble;
                        else if (IsTroubleSet(oldValue)) ZoneStates[zoneIdx] |= ZoneState.Trouble;

                        //zone state changed
                        ZoneStates[zoneIdx] = newZoneState;

                        if (zone.Type != ZoneType.None)
                        {
                            SystemManager.SendZoneUpdateSingleToAll(true, zoneUserValue, zone.Name, ZoneStates[zoneIdx]);

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