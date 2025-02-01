using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MHSApi.API;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Pages;

public partial class HomePage : SecurityPage
{
    private TextBlock[] labels;

    private static SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
    private static SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(57, 151, 102));
    private static SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(204, 55, 0));
    public HomePage()
    {
        InitializeComponent();
        labels = [zone1, zone2, zone3, zone4, zone5, zone6, zone7, zone8, zone9, zone10];
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        UpdateLoadingString("Syncing zone information");
        ShowLoadingBar();
        UpdateZones();
        UpdateLoadingString("Syncing weather information");

        var w = await Services.SecurityClient.FetchWeatherShortData();
        ShortWeatherDataContent? val;
        if (w.IsSuccess && (val = (ShortWeatherDataContent?)w.Value) != null)
        {
            if (string.IsNullOrEmpty(val.WeatherData))
                val.WeatherData = "No weather information";
            
            weatherInfoShort.Text = val.WeatherData;
        }
        else
        {
            weatherInfoShort.Text = "Failed to load weather info";
        }

        HideLoadingBar();
    }

    public override void OnZoneUpdate()
    {
        base.OnZoneUpdate();
        UpdateZones();
    }
    public override void OnConnected()
    {
        base.OnConnected();

        // this fixes a issue where the client is still connecting, and the view is shown before it is connected, and zone information is sent.
        UpdateZones();
        HideLoadingBar();
        Services.MainView.ShowPageTitle();
    }

    private void UpdateZones()
    {
        if (Services.SecurityClient.Zones == null)
            return;
        if (Services.SecurityClient.IsConnected)
        {
            HideLoadingBar();
            int i;
            for (i = 0; i < Services.SecurityClient.Zones.Length; i++)
            {
                var zone = Services.SecurityClient.Zones[i];

                if (zone != null)
                {
                    labels[i].IsVisible = true;
                    labels[i].Text = $"#{i + 1}: {zone.name}";
                    labels[i].Foreground = GetBrushForZoneState(zone);
                }
            }

            // hide unused zones
            for (; i < labels.Length; i++)
            {
                labels[i].IsVisible = false;
            }
        }
        else
        {
            UpdateLoadingString("Connecting to system");
            ShowLoadingBar();
        }
    }

    private SolidColorBrush GetBrushForZoneState(JsonZoneWithReady zone)
    {
        if (zone.type == ZoneType.None)
        {
            return GrayBrush;
        }
        else
        {
            if (zone.ready)
            {
                return GreenBrush;
            }
            else
            {
                return RedBrush;
            }
        }
    }
}