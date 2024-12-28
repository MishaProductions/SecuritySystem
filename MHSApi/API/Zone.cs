using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class JsonZone
    {
        public string name { get; set; } = "Unknown zone";
        public int idx { get; set; }
        public ZoneType type { get; set; }
    }
    public class JsonZoneWithReady : JsonZone
    {
        public bool ready { get; set; }
    }
    public class JsonZones
    {
        public JsonZone[] zones { get; set; } = null!;
    }
    public enum ZoneType
    {
        /// <summary>
        /// No zone is present on the terminal
        /// </summary>
        None = 0,
        /// <summary>
        /// "Special" zone that does not instantly does an alarm
        /// </summary>
        Entry = 1,
        /// <summary>
        /// Zone that instantly causes an alarm if triggered
        /// </summary>
        Window = 2,
        /// <summary>
        /// A smoke detector is connected to this zone
        /// </summary>
        Fire = 3,
    }
}
