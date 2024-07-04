using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class StartAnncRequest
    {
        public string AnncFileName { get; set; } = "";

        public StartAnncRequest(string anncFileName)
        {
            AnncFileName = anncFileName;
        }

        public StartAnncRequest() { }
    }
}
