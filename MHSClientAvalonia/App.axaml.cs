using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using MHSClientAvalonia.Pages;
using MHSClientAvalonia.Utils;
using System;

namespace MHSClientAvalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (!Services.Preferences.GetBool("hideonstartup", false))
                    desktop.MainWindow = new MainWindow();
                else
                {
                    Services.MainWindow = new MainWindow();
                }
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView();
            }

            if (BrowserUtils.IsBrowser)
            {
                Console.WriteLine("MHS Client is running in the browser. Build date: " + new DateTime(Builtin.CompileTime, DateTimeKind.Utc).ToString("MM/dd/yyyy HH:mm:ss"));
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ShowMainWindow_Click(object? sender, System.EventArgs e)
        {
            //Services.MainWindow.Activate(); // TODO: This causes program to exit with code 0, no exceptions are thrown.
            Services.MainWindow.Show();
            Services.MainWindow.Activate();
        }

        private async void Logout_Click(object? sender, System.EventArgs e)
        {
            Services.MainWindow.Show();
            var result = await new ContentDialog()
            {
                Title = "WARNING",
                Content = "Are you sure you want to logout? If you logout and don't log back in, you may no longer safe inside of your home.",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No!"
            }.ShowAsync(Services.MainWindow);

            if (result == ContentDialogResult.Primary)
            {
                Services.Preferences.Set("user_token", "");
                Services.SecurityClient.Stop();
                await Services.MainView.NavigateTo(typeof(LoginPage));
            }
        }

        private async void Quit_Click(object? sender, System.EventArgs e)
        {
            Services.MainWindow.Show();
            var result = await new ContentDialog()
            {
                Title = "WARNING",
                Content = "Are you sure you want to exit MHS Client? If you exit the application, you will not be  safe inside of your home.",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No!"
            }.ShowAsync(Services.MainWindow);

            if (result == ContentDialogResult.Primary)
            {
                Services.MainWindow.ShouldNotClose = false;
                Services.MainWindow.Close();
            }
        }
    }
}