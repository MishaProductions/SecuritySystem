using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Utils
{
    public partial class BrowserUtils
    {
        public static bool IsBrowser
        {
            get
            {
                return RuntimeInformation.RuntimeIdentifier == "browser-wasm";
            }
        }
        [JSImport("window.location.hostname", "main.js")]
        public static partial string GetHost();
    }
}
