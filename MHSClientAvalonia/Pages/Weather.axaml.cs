using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System.Reflection;

namespace MHSClientAvalonia.Pages;

public partial class Weather : SecurityPage
{
    public Weather()
    {
        InitializeComponent();
    }

    public override void OnNavigateTo()
    {
        base.OnNavigateTo();
        

        HideLoadingBar();
    }
}