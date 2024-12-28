using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class SystemInfoResponse
    {
        public string model { get; set; } = "";
        public DateTime fwBuildTime { get; set; }
        public SystemInfoResponse(string model, DateTime fwBuildTime)
        {
            this.model = model;
            this.fwBuildTime = fwBuildTime;
        }
        public SystemInfoResponse() { }
    }
}
