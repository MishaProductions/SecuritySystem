using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System.Reflection;

namespace MHSClientAvalonia.Pages;

public partial class AboutPage : SecurityPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    public override async void OnNavigateTo()
    {
        base.OnNavigateTo();
        TextContents.Children.Clear();

        AddInfo("Client version: " + Assembly.GetExecutingAssembly().GetName().Version);

        try
        {
            var res = await Services.SecurityClient.GetSystemInfo();
            if (res.IsSuccess && res.Value != null)
            {
                var info = (SystemInfoResponse)res.Value;

                AddInfo("System model: " + info.model);

                if (info.fwBuildTime.Year > 2000)
                {
                    AddInfo("Controller build time: " + info.fwBuildTime.ToString("MM/dd/yyyy HH:mm:ss"));
                }
                else
                {
                    AddInfo("Controller build time: Unknown (Controller does not support request)");
                }
            }
        }
        catch
        {

        }

        HideLoadingBar();
    }

    private void AddInfo(string info)
    {
        SelectableTextBlock lbl = new SelectableTextBlock();
        lbl.Text = info;
        lbl.Margin = new Thickness(5);

        TextContents.Children.Add(lbl);
    }
}