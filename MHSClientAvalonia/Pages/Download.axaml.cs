using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System.Reflection;

namespace MHSClientAvalonia.Pages;

public partial class Download : SecurityPage
{
    public Download()
    {
        InitializeComponent();
    }

    public override void OnNavigateTo()
    {
        base.OnNavigateTo();

        lAndroid.NavigateUri = new(Services.SecurityClient.Endpoint + "/client/android/com.mikhailproductions.mhs-Signed.apk");
        lWindows.NavigateUri = new(Services.SecurityClient.Endpoint + "/client/win64/MHSClientAvalonia.Desktop.exe");
        lLinux.NavigateUri = new(Services.SecurityClient.Endpoint + "/client/linux64/MHSClientAvalonia.Desktop");

        HideLoadingBar();
    }
}