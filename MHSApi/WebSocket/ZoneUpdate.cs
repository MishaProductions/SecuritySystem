using MHSApi.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.WebSocket
{
    public class ZoneUpdate : WebSocketMessage
    {
        public override MessageType type => MessageType.ZoneUpdate;
        public JsonZoneWithReady[]? Zones { get; set; }

        public ZoneUpdate() { }
        public ZoneUpdate(JsonZoneWithReady[]? zones)
        {
            Zones = zones;
        }
    }
}
