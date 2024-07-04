using MHSApi.API;

namespace SecuritySystem
{
    public class Zone
    {
        public string Name = "Unnamed zone";
        public ZoneType Type = ZoneType.None;

        /// <summary>
        /// 0 based
        /// </summary>
        public int ZoneNumber = 0;

        public int GpioPin = 0;

        public Zone() { }
        public Zone(int index, string name, ZoneType type, int gpioPin)
        {
            if (index == 0) { throw new InvalidOperationException("Zone # cannot be zero"); }

            this.ZoneNumber = index;
            this.Name = name;
            this.Type = type;
            GpioPin = gpioPin;
        }
    }
}
