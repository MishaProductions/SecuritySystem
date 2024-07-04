using MHSApi.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class SystemStateChange : WebSocketMessage
    {
        public override MessageType type => MessageType.SystemStateChange;
        public bool IsSystemArmed { get; set; }
        public bool IsReady { get; set; }
        public bool IsAlarmState { get; set; }
        public bool IsCountdownInProgress { get; set; }
        public int SystemTimer { get; set; }
        public bool IsEntryDelay { get; set; }
        public SystemStateChange() { }

        public SystemStateChange(bool isSystemArmed, bool isReady, bool isAlarmState, bool isCountdownInProgress, int systemTimer, bool isEntryDelay)
        {
            IsSystemArmed = isSystemArmed;
            IsReady = isReady;
            IsAlarmState = isAlarmState;
            IsCountdownInProgress = isCountdownInProgress;
            SystemTimer = systemTimer;
            IsEntryDelay = isEntryDelay;
        }
    }
}
