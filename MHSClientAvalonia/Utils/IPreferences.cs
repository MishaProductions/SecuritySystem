using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Utils
{
    public abstract class IPreferences
    {
        public abstract string Get(string key);
        public abstract void Set(string key, string value);
        public bool GetBool(string key, bool defaultValue)
        {
            // set default value
            if (Get(key) == null)
            {
                Set(key, defaultValue ? "yes" : "no");
            }

            return Get(key) == "yes" ? true : false;
        }
        public void SetBool(string key, bool value)
        {
            Set(key, value ? "yes" : "no");
        }
    }
}
