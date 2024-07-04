using SecuritySystem.Modules.NXDisplay;

namespace SecuritySystem.Modules
{
    public abstract class Module
    {
        /// <summary>
        /// Add event handlers here
        /// </summary>
        public abstract void OnRegister();
        /// <summary>
        /// Preform clean up here. For example, power off devices, unregister events, etc
        /// </summary>
        public abstract void OnUnregister();
    }

    public static class ModuleController
    {
        private static List<Module> _Modules = new();
        public static List<Module> Modules { get { return _Modules; } }
        public static void RegisterModule(Module mod)
        {
            Modules.Add(mod);
            mod.OnRegister();
        }

        public static void UnregisterModule(Module mod)
        {
            Modules.Remove(mod);
        }

        public static NextionDisplay[] GetDisplays()
        {
            List<NextionDisplay> displays = new List<NextionDisplay>();
            foreach (Module m in Modules)
            {
                if (m is NextionDisplay d)
                {
                    displays.Add(d);
                }
            }

            return displays.ToArray();
        }
    }
}
