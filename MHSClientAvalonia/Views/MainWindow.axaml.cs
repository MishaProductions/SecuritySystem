using Avalonia.Controls;
using FluentAvalonia.UI.Windowing;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia
{
    public partial class MainWindow : AppWindow
    {
        public bool ShouldNotClose = true;
        public MainWindow()
        {
            InitializeComponent();

            Services.MainWindow = this;
        }

        private void AppWindow_Closing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
        {
            if (e.CloseReason == WindowCloseReason.WindowClosing)
            {
                e.Cancel = ShouldNotClose;
                if (ShouldNotClose)
                {
                    Hide();
                }
            }
        }
    }
}