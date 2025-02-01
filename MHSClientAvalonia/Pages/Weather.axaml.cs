using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System.Reflection;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class Weather : SecurityPage
{
    public Weather()
    {
        InitializeComponent();
    }

    public override async Task OnNavigateTo()
    {
        await base.OnNavigateTo();

        HideLoadingBar();
    }
}