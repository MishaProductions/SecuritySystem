using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class FwUpdateMsg : WebSocketMessage
    {
        public override MessageType type => MessageType.FwUpdate;
        public string DeviceName { get; set; } = "";
        public string UpdateProgressDescription { get; set; } = "";
        public int Percent { get; set; }

        public FwUpdateMsg() { }
        public FwUpdateMsg(string devName, string desc, int percentCompletion)
        {
            DeviceName = devName;
            Percent = percentCompletion;
            UpdateProgressDescription = desc;
        }
    }
}
