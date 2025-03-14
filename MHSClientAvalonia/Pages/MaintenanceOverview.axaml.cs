using System.Threading.Tasks;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Pages;

public partial class MaintenanceOverview : SecurityPage
{
    public MaintenanceOverview()
    {
        InitializeComponent();
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();
        HideLoadingBar();
    }

    private async void FirmwareUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(FirmwareUpdate));
    }
    private async void EventLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo(typeof(EventLog));
    }
}