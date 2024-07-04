using Avalonia;
using System;
using DesktopNotifications1.Avalonia;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Desktop
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .SetupDesktopNotifications(out Services.NotificationManager!)
                .LogToTrace();
    }
}
