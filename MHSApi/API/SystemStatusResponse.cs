/*
 * PROJECT: SecuritySystem
 * DATE: 2/6/2024
 * DESCRIPTION: Contains System status api response content
*/

namespace MHSApi.API
{
    /// <summary>
    /// The response content of the system status API
    /// </summary>
    public class SystemStatusResponse
    {
        public bool SystemArmed { get; set; }
        public int Timer { get; set; }
        public bool EntryDelay { get; set; }
        public bool ExitDelay { get; set; }

        public SystemStatusResponse(bool systemArmed, int timer, bool entryDelay, bool exitDelay)
        {
            SystemArmed = systemArmed;
            Timer = timer;
            EntryDelay = entryDelay;
            ExitDelay = exitDelay;
        }
    }
}
