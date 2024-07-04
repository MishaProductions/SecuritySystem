using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using MHSClientAvalonia.Utils;
using SecuritySystemApi;

namespace MHSClientAvalonia.Pages;

public partial class EventLog : SecurityPage
{
    public EventLog()
    {
        InitializeComponent();
    }
    public override async void OnNavigateTo()
    {
        base.OnNavigateTo();

        UpdateLoadingString("Loading event log");
        var data = await Services.SecurityClient.GetEventLog();
        if (data.IsSuccess && data.Value != null)
        {
            HideLoadingBar();
            TargetDataGrid.ItemsSource = (EventLogEntry[])data.Value;
        }
        else
        {
            HideLoadingBar();
            await new ContentDialog() { Title = "Failed to load event log", Content = "Check connection, or server might not support this request. Error: " + data.ResultMessage, PrimaryButtonText = "OK" }.ShowAsync();
        }
    }
}