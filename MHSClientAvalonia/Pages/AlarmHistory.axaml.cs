using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using MHSClientAvalonia.Utils;
using SecuritySystemApi;

namespace MHSClientAvalonia.Pages;

public partial class AlarmHistory : SecurityPage
{
    public AlarmHistory()
    {
        InitializeComponent();
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();

        UpdateLoadingString("Loading alarm history");
        var data = await Services.SecurityClient.GetAlarmHistory();
        if (data.IsSuccess && data.Value != null)
        {
            HideLoadingBar();
            TargetDataGrid.ItemsSource = (AlarmHistoryInfoContent[])data.Value;
        }
        else
        {
            HideLoadingBar();
            await new ContentDialog() { Title = "Something went wrong", Content = "Failed to load alarm history: " + data.ResultMessage, PrimaryButtonText = "OK" }.ShowAsync();
        }
    }
}