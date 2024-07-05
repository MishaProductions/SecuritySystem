using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem.DeviceSubsys
{
    public static class DeviceModel
    {
        public static event LegacyDeviceModelOnFirmwareUpdateProgressEventArgs? FirmwareUpdateEvent;
        public static void InitializeAll()
        {

        }
        public static void BroadcastFwUpdateProgress(string devName, string desc, int percent)
        {
            FirmwareUpdateEvent?.Invoke(devName, desc, percent);
        }
    }

    public delegate void LegacyDeviceModelOnFirmwareUpdateProgressEventArgs(string devName, string desc, int percent);
}
