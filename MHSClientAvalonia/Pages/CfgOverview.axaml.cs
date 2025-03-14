using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSClientAvalonia.Utils;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class CfgOverview : SecurityPage
{
    public CfgOverview()
    {
        InitializeComponent();
    }
    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        HideLoadingBar();
    }
    private async void Zones_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(ZonesCfg));
    }
    private async void Users_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(UsersCfg));
    }
    private async void NotificationSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(NotificationsCfg));
    }
    private async void About_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(AboutPage));
    }
}