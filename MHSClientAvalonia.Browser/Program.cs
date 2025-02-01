using Avalonia;
using Avalonia.Browser;
using MHSClientAvalonia;
using MHSClientAvalonia.Browser;
using MHSClientAvalonia.Utils;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        try
        {
            Services.AudioCapture = new BrowserAudioCapture();
            return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return Task.CompletedTask;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}