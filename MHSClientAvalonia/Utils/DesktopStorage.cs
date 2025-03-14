using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MHSApi.API;

namespace MHSClientAvalonia.Utils
{
    internal class DesktopStorage : IPreferences
    {
        private static readonly SemaphoreSlim Sema = new(1, 1);
        private static IsolatedStorageFile Store => IsolatedStorageFile.GetUserStoreForDomain();
        public override string Get(string key)
        {
            Sema.Wait();

            // it may happen, that a value type changes and can't be deserialized
            // so prevent exceptions in this case
            try
            {
                using var stream = Store.OpenFile(key, FileMode.Open);
                return JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.String) ?? "";
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                Sema.Release();
            }
        }

        public override void Set(string key, string value)
        {
            Sema.Wait();
            try
            {
                using var stream = Store.OpenFile(key, FileMode.Create, FileAccess.Write);
                JsonSerializer.Serialize(stream, value, SourceGenerationContext.Default.String);
            }
            catch (Exception)
            {
            }
            finally
            {
                Sema.Release();
            }
        }
    }
}
