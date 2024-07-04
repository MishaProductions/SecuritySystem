using System.Threading.Tasks;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Pages;

public partial class MaintenanceOverview : SecurityPage
{
    public MaintenanceOverview()
    {
        InitializeComponent();
    }

    public override void OnNavigateTo()
    {
        base.OnNavigateTo();
        HideLoadingBar();
    }

    private async void FirmwareUpdate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo("FirmwareUpdate");
    }
    private async void EventLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Services.MainView.NavigateTo("FirmwareUpdate");
    }
}