using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using DesktopNotifications;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MHSClientAvalonia.Client;
using MHSClientAvalonia.Pages;
using MHSClientAvalonia.Utils;
using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;

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