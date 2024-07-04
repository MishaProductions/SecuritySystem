using SecuritySystem.Modules;
using SecuritySystem.Modules.NXDisplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem.Utils
{
    /// <summary>
    /// Contains common events
    /// </summary>
    public static class SystemManager
    {
        public static event AlarmEventHandler? OnAlarm;
        public static event ZoneUpdateEventHandler? OnZoneUpdate;
        public static event EventHandler? OnSystemDisarm;
        public static event SysTimerEvent? OnSysTimerEvent;
        public static void StartAlarm(int zone)
        {
            OnAlarm?.Invoke(zone);
        }

        public static void SendZoneUpdateSingleToAll(bool single, int zone, string name, ZoneState ready)
        {
            OnZoneUpdate?.Invoke(single, zone, name, ready);
        }

        internal static void InvokeSystemDisarm()
        {
            OnSystemDisarm?.Invoke(null, new EventArgs());
        }

        internal static void SendSysTimer(bool entry, int timer)
        {
            OnSysTimerEvent?.Invoke(entry, timer);
        }
        /// <summary>
        /// Disarms the system. This method does not send any events, it asssumes that the System Timer thread is working properly.
        /// </summary>
        internal static void DisarmSystem()
        {
            // disarm the system
            Configuration.Instance.SystemArmed = false;
            Configuration.Instance.Timer = 15;
            Configuration.Instance.InEntryDelay = false;
            Configuration.Instance.InExitDelay = false;
            Configuration.Instance.SystemAlarmState = false;
            Configuration.Instance.IsZoneOpenedWhenSystemArmed = false;
            Configuration.Save();
        }
        /// <summary>
        /// Begins the arming process. This method does not send any events as it assumes that the SysTimer thread works properly
        /// </summary>
        internal static void ArmSystem()
        {
            Configuration.Instance.Timer = 15;
            Configuration.Instance.InExitDelay = true;
            Configuration.Instance.SystemArmed = true;
            Configuration.Save();
        }

        public delegate void ZoneUpdateEventHandler(bool single, int zone, string name, ZoneState ready);
        public delegate void AlarmEventHandler(int alarmZone);
        public delegate void SysTimerEvent(bool entry, int timer);

        public static void WriteToEventLog(string message, User? user = null)
        {
            var evt = new SecuritySystemApi.EventLogEntry() { date = DateTime.Now.ToString(), Message = message };

            if (user != null)
            {
                evt.Username = user.Username;
            }

            Configuration.Instance.EventLog.Add(evt);
            Configuration.Save();
        }
    }
}
