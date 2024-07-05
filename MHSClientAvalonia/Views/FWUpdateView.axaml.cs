using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MHSClientAvalonia;

public partial class FWUpdateView : UserControl
{
    public int ProgressPercentage
    {
        set
        {
            MainProgress.IsIndeterminate = value == 0;
            MainProgress.Value = value;

            LblPercent.Text = $"{value}% complete";
        }
    }
    public string ProgressDesc
    {
        set
        {
            LblDesc.Text = value;
        }
    }
    public string ProgressDeviceName
    {
        set
        {
            LblTitle.Text = $"The requested firmware update for {value} is being completed. ";
        }
    }
    public FWUpdateView()
    {
        InitializeComponent();
    }
}