using System;
using System.Runtime.InteropServices.JavaScript;

namespace MHSClientAvalonia.Utils
{
    public partial class BrowserStorage : IPreferences
    {

        [JSImport("globalThis.localStorage.clear")]
        public static partial string LocalStorageClear();
        [JSImport("globalThis.localStorage.getItem")]
        public static partial string LocalStorageGetItem(string key);
        [JSImport("globalThis.localStorage.setItem")]
        public static partial string LocalStorageSetItem(string key, string? val);

        public override void Set(string key, string value)
        {
            LocalStorageSetItem(key, value);
        }

        public override string Get(string key)
        {
            return LocalStorageGetItem(key);
        }
    }
}
