using SecuritySystemApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace MHSApi.API
{
    public class FirmwareUpdateProgressResponseContent : ApiResponseContent
    {
        public bool inprogress { get; set; }
        public bool fail { get; set; }
        public int progress { get; set; }
        public string progressstring { get; set; } = "";
        public bool finished { get; set; }

        public FirmwareUpdateProgressResponseContent(bool inprogress, bool fail, int progress, string progressstring, bool finished)
        {
            this.inprogress = inprogress;
            this.fail = fail;
            this.progress = progress;
            this.progressstring = progressstring;
            this.finished = finished;
        }

        public FirmwareUpdateProgressResponseContent() { }
    }
}
