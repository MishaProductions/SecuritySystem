using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MHSClientAvalonia;

public partial class FwUpdateWindow : Window
{
    public FWUpdateView UpdaetView;
    public FwUpdateWindow()
    {
        InitializeComponent();
        UpdaetView = FwUpdateView;
    }
}