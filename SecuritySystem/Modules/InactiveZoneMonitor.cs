using System.Device.Gpio;
using System.Timers;
using SecuritySystem.Modules.NXDisplay;
using SecuritySystem.Utils;

namespace SecuritySystem.Modules
{
    public class InactiveZoneMonitor : Module
    {
        private Dictionary<int, DateTime> LastReadyZone = []; // <Zone index, Last Ready>
        private Dictionary<int, DateTime> IgnoreZones = []; // <Zone index, Ignore until>
        private System.Timers.Timer ZoneTimer = new();
        public override void OnRegister()
        {
            // TODO: maybe save this if system restarts
            for(int i = 0; i < ZoneController.ZoneStates.Length; i++)
            {
                LastReadyZone.Add(i, DateTime.Now);
            }

            SystemManager.OnZoneUpdate += SystemManager_OnZoneUpdate;
            SystemManager.OnInactiveZoneIgnore += SystemManager_OnInactiveZoneIgnore;

            ZoneTimer.Elapsed += TimerCB;
            ZoneTimer.Interval = 60000;
            ZoneTimer.Start();
        }

        public override void OnUnregister()
        {
            SystemManager.OnZoneUpdate -= SystemManager_OnZoneUpdate;
        }

        private void SystemManager_OnInactiveZoneIgnore(int zone)
        {
            Console.WriteLine("InactiveZoneMT: OnInactiveZoneIgnore: "+ zone);
            DateTime ignoreUntil = DateTime.Now.AddHours(4);
            if(!IgnoreZones.ContainsKey(zone))
                IgnoreZones.Add(zone, ignoreUntil);
            else
                IgnoreZones[zone] = ignoreUntil;
        }

        private void SystemManager_OnZoneUpdate(bool single, int zone, string name, ZoneState ready)
        {
            if (single)
            {
                if (ready == ZoneState.Ready)
                    LastReadyZone[zone] = DateTime.Now;
            }
            else
            {
                // TODO handle zone size changes
            }
        }

        private void TimerCB(object? sender, ElapsedEventArgs e)
        {
            foreach (var item in LastReadyZone)
            {
                var lastReadyDur = DateTime.Now - item.Value;
                if (lastReadyDur >= TimeSpan.FromMinutes(10))
                {
                    if (ZoneController.ZoneStates[item.Key] == PinValue.Low)
                    {
                        LastReadyZone[item.Key] = DateTime.Now;
                        continue;
                    }

                    if (IgnoreZones.ContainsKey(item.Key))
                    {
                        if (IgnoreZones[item.Key] <= DateTime.Now)
                        {
                            Console.WriteLine("InactiveZoneMT: remove expired entry for zone " + item.Key);
                            IgnoreZones.Remove(item.Key);
                        }
                        else
                        {
                            // Ignore zones entry is valid, ignore it.
                            continue;
                        }
                    }

                    Console.WriteLine($"InactiveZoneMT: ***Found inactive zone {item.Key} since {lastReadyDur} time");
                    var idx = MusicPlayer.AnncFiles.IndexOf("standclear.mp3");
                    if (idx != -1)
                        MusicPlayer.PlayAnnc(idx);

                    SystemManager.InactiveZone(item.Key, lastReadyDur);
                }
            }
        }
    }
}