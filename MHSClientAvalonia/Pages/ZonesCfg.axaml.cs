using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSClientAvalonia.Utils;
using Avalonia.Layout;
using MHSApi.API;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class ZonesCfg : SecurityPage
{
    public ZonesCfg()
    {
        InitializeComponent();
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        ShowLoadingBar();
        UpdateLoadingString("Loading zone information");

        ZonesContainer.Children.Clear();
        if (Services.SecurityClient.Zones == null)
        {
            MainView.ShowMessage("System error", "Invaild system state");
            HideLoadingBar();
            return;
        }

        int i = 0;
        foreach (var item in Services.SecurityClient.Zones)
        {
            var wrap = new WrapPanel
            {
                Margin = new(5),
                Tag = i
            };

            // zone number
            wrap.Children.Add(new TextBlock() { Text = $"{item.idx + 1}", VerticalAlignment = VerticalAlignment.Center });
            wrap.Children.Add(new TextBox() { Text = item.name, Width = 200, Margin = new(5) });
            wrap.Children.Add(new ComboBox()
            {
                ItemsSource = new ComboBoxItem[]
                {
                    new() { Content = "Unconfigured" },
                    new() { Content = "Entry" },
                    new() { Content = "Window" },
                    new() { Content = "Motion" },
                },
                SelectedIndex = (int)item.type,
                Width = 100,
                Margin = new(5)
            }
            );

            ZonesContainer.Children.Add(wrap);
            i++;
        }

        HideLoadingBar();
    }

    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateLoadingString("Saving zone information");
        ShowLoadingBar();
        if (Services.SecurityClient.Zones == null)
        {
            MainView.ShowMessage("System error", "Invaild system state");
            HideLoadingBar();
            return;
        }

        JsonZone[] zones = new JsonZone[Services.SecurityClient.Zones.Length];

        foreach (Control zoneThing in ZonesContainer.Children)
        {
            if (zoneThing is WrapPanel panel)
            {
                if (panel.Tag is int zoneIdx)
                {
                    TextBox name = (TextBox)panel.Children[1];
                    ComboBox cmb = (ComboBox)panel.Children[2];

                    if (string.IsNullOrEmpty(name.Text))
                    {
                        HideLoadingBar();
                        MainView.ShowMessage("Name for zone " + (zoneIdx + 1) + " cannot be empty or null.", "Zone name is empty");
                        return;
                    }

                    zones[zoneIdx] = new JsonZone
                    {
                        name = name.Text,
                        idx = zoneIdx,
                        type = (ZoneType)cmb.SelectedIndex
                    };
                }
            }
        }

        // send the new zones to server
        var resp = await Services.SecurityClient.SetZoneSettings(new JsonZones() { zones = zones });

        if (!resp.IsSuccess)
        {
            MainView.ShowMessage(resp.ResultMessage, "Server error");
        }

        HideLoadingBar();
    }
    private async void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OnNavigateTo();
    }
}