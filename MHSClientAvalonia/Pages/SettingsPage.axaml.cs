using MHSClientAvalonia.Utils;
using Microsoft.Win32;
using System;
using System.Reflection;

namespace MHSClientAvalonia.Pages;

public partial class SettingsPage : SecurityPage
{
    public SettingsPage()
    {
        InitializeComponent();

        SettingsExpander1.Header = "Version: " + Assembly.GetExecutingAssembly().GetName().Version;

        AutomaticUpdateCheck.IsChecked = Services.Preferences.GetBool("autoupdate", true);
        ZoneNoiseCheck.IsChecked = Services.Preferences.GetBool("zonenoise", true);
        BeepCheck.IsChecked = Services.Preferences.GetBool("armnoise", true);
        StartMinmized.IsChecked = Services.Preferences.GetBool("hideonstartup", false);

        if (BrowserUtils.IsBrowser)
        {
            SettingsExpanderWindowsStart.IsVisible = false;
            SettingsExpanderStartMinimized.IsVisible = false;
        }
        else
        {
            SettingsExpanderWindowsStart.IsVisible = OperatingSystem.IsWindows();
            if (OperatingSystem.IsWindows())
            {
                StartWithWindows.IsChecked = GetStartup();
            }
        }
    }

    public override void OnNavigateTo()
    {
        base.OnNavigateTo();
        HideLoadingBar();
    }
    private void CheckForUpdatesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        UpdateChkText.Text = "Checking for updates";
        runner.IsVisible = true;

        UpdateChecker.DoCheck(true, delegate
        {
            CheckForUpdatesButton.IsEnabled = true;
            UpdateChkText.Text = "Check for updates";
            runner.IsVisible = false;
        });
    }

    private void AutomaticUpdateCheck_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Services.Preferences.SetBool("autoupdate", AutomaticUpdateCheck.IsChecked.GetValueOrDefault());
    }

    private void ZoneNoiseCheck_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Services.Preferences.SetBool("zonenoise", ZoneNoiseCheck.IsChecked.GetValueOrDefault());
    }

    private void BeepCheck_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Services.Preferences.SetBool("armnoise", BeepCheck.IsChecked.GetValueOrDefault());
    }

    private void StartMinimized_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Services.Preferences.SetBool("hideonstartup", StartMinmized.IsChecked.GetValueOrDefault());
    }
    private void StartWithWindows_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetStartup(StartWithWindows.IsChecked.GetValueOrDefault());
    }


    private static bool GetStartup()
    {
        if (OperatingSystem.IsWindows())
        {
            RegistryKey? rk = Registry.CurrentUser.OpenSubKey
       ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (rk == null) return false;

            return rk.GetValue("MHS Security Center") != null;
        }
        else
        {
            return false;
        }
    }
    private static void SetStartup(bool startWithWindows)
    {
        if (OperatingSystem.IsWindows())
        {
            RegistryKey? rk = Registry.CurrentUser.OpenSubKey
       ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (rk == null) return;

            if (startWithWindows)
                rk.SetValue("MHS Security Center", System.Reflection.Assembly.GetExecutingAssembly().Location);
            else
                rk.DeleteValue("MHS Security Center", false);
        }
    }
}