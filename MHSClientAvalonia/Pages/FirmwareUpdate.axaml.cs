using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MHSClientAvalonia.Pages;

public partial class FirmwareUpdate : SecurityPage
{
    public FirmwareUpdate()
    {
        InitializeComponent();
    }
    public override void OnNavigateTo()
    {
        base.OnNavigateTo();
        HideLoadingBar();
    }
}