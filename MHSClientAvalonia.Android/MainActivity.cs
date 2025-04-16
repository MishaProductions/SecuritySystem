using Android.App;
using Android.Content.PM;

using Avalonia;
using Avalonia.Android;
using MHSClientAvalonia;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Android;

[Activity(
    Label = "MHSClientAvalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Services.AudioCapture = new AndroidAudioCapture(this);
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
