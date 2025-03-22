using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using DesktopNotifications;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using MHSApi.API;
using MHSClientAvalonia.Pages;
using MHSClientAvalonia.Utils;
using MHSClientAvalonia.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MHSClientAvalonia;

public partial class MainView : UserControl
{
    private readonly DispatcherTimer _reconnectTimer = new();
    private readonly DispatcherTimer _updateTimer = new();
    internal Visual VisualForUpdate { get { return (Visual?)VisualRoot ?? throw new Exception(); } }
    private static FwUpdateWindow? FwUpdateWindow;
    public MainView()
    {
        InitializeComponent();
        Loaded += OnLoad;

        Services.MainView = this;
        Services.SecurityClient.OnWSClose += SecurityClient_OnWSClose;
        Services.SecurityClient.OnAuthenticationFailure += SecurityClient_OnAuthenticationFailure;
        Services.SecurityClient.OnZoneUpdate += SecurityClient_OnZoneUpdate;
        Services.SecurityClient.OnConnected += SecurityClient_OnConnected;
        Services.SecurityClient.OnSystemTimerEvent += SecurityClient_OnSystemTimerEvent;
        Services.SecurityClient.OnSystemDisarm += SecurityClient_OnSystemDisarm;
        Services.SecurityClient.OnMusicVolChanged += SecurityClient_OnMusicVolChanged;
        Services.SecurityClient.OnAnncVolChanged += SecurityClient_OnAnncVolChanged;
        Services.SecurityClient.OnFwUpdateProgress += SecurityClient_OnFwUpdateProgress;
        Services.SecurityClient.OnMusicStarted += SecurityClient_OnMusicStarted;
        Services.SecurityClient.OnMusicStopped += SecurityClient_OnMusicStopped;
        Services.SecurityClient.OnAnncStarted += SecurityClient_OnAnncStarted;
        Services.SecurityClient.OnAnncStopped += SecurityClient_OnAnncStopped;
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            var toplevel = TopLevel.GetTopLevel(this);

            if (toplevel != null)
            {
                var insetsManager = toplevel.InsetsManager;

                if (insetsManager != null)
                {
                    insetsManager.DisplayEdgeToEdge = false;
                    insetsManager.IsSystemBarVisible = false;
                }
            }


            Margin = new Thickness(5);
        }

        navigationView.IsPaneVisible = false;
        PageTitle.IsVisible = false;
        _reconnectTimer.Interval = TimeSpan.FromSeconds(5);
        _reconnectTimer.Tick += ReconnectTimer_Tick;
        navigationView.BackRequested += OnNavigationViewBackRequested;
        FrameView.Navigated += FrameView_Navigated;

        if (!BrowserUtils.IsBrowser && Services.Preferences.GetBool("autoupdate", true))
        {
            _updateTimer.Interval = TimeSpan.FromMinutes(5);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }
        UiAndroidHint.IsOpen = BrowserUtils.IsBrowser && Services.Preferences.GetBool("showandroidhint", true);

        await NavigateToInitialPage();
    }

    private void AndroidHint_CloseButtonClick(InfoBar sender, EventArgs e)
    {
        Services.Preferences.SetBool("showandroidhint", false);
    }
    private void SecurityClient_OnFwUpdateProgress(MHSApi.WebSocket.FwUpdateMsg msg)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            if (!BrowserUtils.IsBrowser)
            {
                if (FwUpdateWindow == null)
                {
                    FwUpdateWindow = new();
                    FwUpdateWindow.Show();
                    FwUpdateWindow.Activate();
                }
                FwUpdateWindow.FwUpdateView.ProgressDesc = msg.UpdateProgressDescription;
                FwUpdateWindow.FwUpdateView.ProgressDeviceName = msg.DeviceName;
                FwUpdateWindow.FwUpdateView.ProgressPercentage = msg.Percent;

                if (msg.Percent == 100)
                {
                    FwUpdateWindow.Close();
                    FwUpdateWindow = null;
                }
            }
        });
    }
    private void SecurityClient_OnMusicStarted(string fileName, bool isLive)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnMusicFileChanged(fileName, isLive);
        });
    }
    private void SecurityClient_OnMusicStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnMusicFileChanged(null, false);
        });
    }
    private void SecurityClient_OnAnncStarted(string fileName, bool isLive)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnAnncChanged(fileName, isLive);
        });
    }
    private void SecurityClient_OnAnncStopped(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnAnncChanged(null, false);
        });
    }
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!Services.Preferences.GetBool("autoupdate", true))
            return;
        UpdateChecker.DoCheck(false);
    }
    private SecurityPage? GetCurrentPage()
    {
        if (FrameView.Content is SecurityPage page)
        {
            return page;
        }
        return null;
    }

    private void OnNavigationViewBackRequested(object? sender, NavigationViewBackRequestedEventArgs e)
    {
        FrameView.GoBack();
    }
    private void SecurityClient_OnSystemDisarm(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(delegate
      {
          var page = GetCurrentPage();
          page?.OnSystemDisarm();

          SendNotification("The system is disarmed");
          UpdateSystemStatus();
      });
    }

    private void UpdateSystemStatus()
    {
        ArmButton.IsEnabled = true;
        if (Services.SecurityClient.IsSysArmed)
        {
            if (Services.SecurityClient.IsAlarmState)
            {
                SystemStatusLabel.Text = "System Status: ALARM";
                ArmButton.Content = "Disarm";
            }
            else
            {
                SystemStatusLabel.Text = "System Status: Armed";
                ArmButton.Content = "Disarm";
            }
        }
        else
        {
            if (Services.SecurityClient.IsReady)
            {
                SystemStatusLabel.Text = "System Status: Ready";
                ArmButton.Content = "Arm";
            }
            else
            {
                SystemStatusLabel.Text = "System Status: Not ready";
                ArmButton.IsEnabled = false;
            }
        }
    }

    private void SecurityClient_OnSystemTimerEvent(bool arming, int time)
    {
        Dispatcher.UIThread.Invoke(async delegate
        {
            if (FrameView.Content is SecurityPage page)
            {
                page.OnSysTimer(arming, time);
            }
            await PlaySysTimer();


            if (arming)
            {
                if (time > 1)
                    SystemStatusLabel.Text = "System Status: Arming (in " + time + " seconds)";
                else if (time == 1)
                    SystemStatusLabel.Text = "System Status: Arming (in " + time + " second)";
                else
                {
                    SystemStatusLabel.Text = "System Status: Armed";
                    SendNotification("The security system is armed");
                }

                ArmButton.Content = "Disarm system";
            }
            else
            {
                if (time > 1)
                    SystemStatusLabel.Text = "System Status: ALARM in " + time + " seconds";
                else if (time == 1)
                    SystemStatusLabel.Text = "System Status: ALARM in " + time + " second";
                else
                {
                    SystemStatusLabel.Text = "System Status: ALARM";
                    SendNotification("The security system is in the ALARM STATE. You (hopefully) forgot forgot to disarm it!");
                }

                ArmButton.Content = "Disarm system";
            }
        });
    }

    private void SecurityClient_OnAnncVolChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnAnncVolChanged();
        });
    }

    private void SecurityClient_OnMusicVolChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(delegate
        {
            var page = GetCurrentPage();
            page?.OnMusicVolChanged();
        });
    }
    public static async void ShowMessage(string title, string content)
    {
        await new ContentDialog()
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
        }.ShowAsync();
    }
    private async void SecurityClient_OnAuthenticationFailure(object? sender, EventArgs e)
    {
        await new ContentDialog()
        {
            Title = "Something went wrong",
            Content = "Something went wrong while authenticating with the websocket. Press OK to visit the login page. If that does not work, try to restart your security system controller, update your client, or try a firmware update.",
            CloseButtonText = "OK",
        }.ShowAsync();
        await NavigateTo(typeof(LoginPage));
    }
    private void SecurityClient_OnZoneUpdate(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(async delegate
        {
            var page = GetCurrentPage();
            page?.OnZoneUpdate();
            await PlayZoneSound();
            UpdateSystemStatus();
        });
    }
    private void SecurityClient_OnConnected(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(async delegate
        {
            var page = GetCurrentPage();
            page?.OnConnected();

            UpdatePageVisibility();

            // update label
            UpdateSystemStatus();

            if (Services.SecurityClient.CurrentUser != null)
            {
                UserPopupButton.Content = Services.SecurityClient.CurrentUser.Username;

                if (Services.SecurityClient.CurrentUser.Username == "System Installer")
                {
                    await NavigateTo(typeof(InitialSetup), true);
                }
            }
            else
            {
                UserPopupButton.Content = "Unknown user";
            }

            SendNotification("MHS Client has successfully connected to the device");


        });
    }

    private void UpdatePageVisibility()
    {
        ZonesTab.IsVisible = Services.SecurityClient.CurrentUser?.Permissions == UserPermissions.Admin;
        NotificationsTab.IsVisible = Services.SecurityClient.CurrentUser?.Permissions == UserPermissions.Admin;
        UsersTab.IsVisible = Services.SecurityClient.CurrentUser?.Permissions == UserPermissions.Admin;
        FwUpdateTab.IsVisible = Services.SecurityClient.CurrentUser?.Permissions == UserPermissions.Admin;
    }

    private void SecurityClient_OnWSClose(object? sender, System.EventArgs e)
    {
        Console.WriteLine("start reconnect timer");
        Dispatcher.UIThread.Invoke(delegate
        {
            ReconnectDialogue.IsOpen = true;
            _reconnectTimer.Start();
            SendNotification("MHS client has disconnected from the device. Trying to connect...");
        });
    }
    private static async void SendNotification(string title)
    {
        var nf = new Notification
        {
            Title = "MHS Client",
            Body = title,
        };

        try
        {
            if (Services.NotificationManager != null)
                await Services.NotificationManager.ShowNotification(nf, DateTimeOffset.Now.AddSeconds(3));
        }
        catch
        {

        }
    }
    private static async Task PlaySysTimer()
    {
        if (!Services.Preferences.GetBool("armnoise", true))
            return;
        var wavfile = AssetLoader.Open(new Uri("avares://MHSClientAvalonia/Assets/systimer.wav"));
        await PlaySound(wavfile);
    }
    private static async Task PlayZoneSound()
    {
        if (!Services.Preferences.GetBool("zonenoise", true))
            return;
        var wavfile = AssetLoader.Open(new Uri("avares://MHSClientAvalonia/Assets/zone.wav"));
        await PlaySound(wavfile);
    }
    private static async Task PlaySound(Stream wavfile)
    {
        if (Services.AudioPlayBack != null)
        {
            await Services.AudioPlayBack.PlayWav(wavfile);
        }
    }
    private async void ReconnectTimer_Tick(object? sender, EventArgs e)
    {
        var token = Services.Preferences.Get("user_token");
        if (token != null)
        {
            try
            {
                if (await Services.SecurityClient.Start(token) == SecurityApiResult.Success)
                {
                    _reconnectTimer.Stop();
                    ReconnectDialogue.IsOpen = false;
                }
            }
            catch
            {

            }
        }
    }

    private async void ArmDisarmBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AutoCompleteBox box = new();
        var dialog = new ContentDialog
        {
            Title = "Enter passcode",
            PrimaryButtonText = "Submit",
            CloseButtonText = "Cancel",
            Content = box
        };
        box.ItemsSource = new List<string>() { "1234" };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (box.Text != null)
            {
                var result2 = await Services.SecurityClient.ArmOrDisarm(box.Text);
                if (result2 != null)
                {
                    if (result2.IsSuccess)
                    {
                        // success
                        await new ContentDialog()
                        {
                            Title = result2.ResultMessage,
                            CloseButtonText = "Ok"
                        }.ShowAsync();
                    }
                    else
                    {
                        // failure
                        await new ContentDialog()
                        {
                            Title = result2.ResultMessage,
                            CloseButtonText = "Ok"
                        }.ShowAsync();
                        ArmDisarmBtn_Click(sender, e);
                    }
                }
                else
                {
                    // failure
                    await new ContentDialog()
                    {
                        Title = "server communication failure",
                        CloseButtonText = "Ok"
                    }.ShowAsync();
                    ArmDisarmBtn_Click(sender, e);
                }
            }
            else
            {
                await new ContentDialog()
                {
                    Title = "You must enter a passcode",
                    CloseButtonText = "Ok"
                }.ShowAsync();
                ArmDisarmBtn_Click(sender, e);
            }
        }
    }

    private void CurrentUser_Clicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control ctl)
        {
            FlyoutBase.ShowAttachedFlyout(ctl);
        }
    }
    public void ShowPageTitle()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()) return;
        PageTitle.IsVisible = true;
    }
    /// <summary>
    /// Navigates to page by tag
    /// </summary>
    /// <param name="page">The page tag</param>
    public async Task NavigateTo(Type page, bool clearBackStack = false)
    {
        if (page == null)
            return;
        Console.WriteLine("navigate to " + page);
        try
        {
            // send navigate away event to old page
            if (navigationView.Content is SecurityPage oldPage)
            {
                await oldPage.OnNavigateAway();
            }

            if (page == typeof(LoginPage))
            {
                PageTitle.Text = "Login";
                PageTitle.IsVisible = true;
                navigationView.IsPaneVisible = false;
                pnlControls.IsVisible = false;
                if (!FrameView.Navigate(typeof(LoginPage), "", new SlideNavigationTransitionInfo()))
                {
                    Console.WriteLine("Navigation failed");
                }

                runnerBox.IsVisible = false;
                MainFrameBox.IsVisible = true;
            }
            else
            {
                if (page == typeof(InitialSetup))
                {
                    PageTitle.Text = "System setup";
                    PageTitle.IsVisible = true;
                    navigationView.IsPaneVisible = false;
                    pnlControls.IsVisible = false;
                }
                else
                {
                    navigationView.IsPaneVisible = true;
                    pnlControls.IsVisible = true;

                    runnerBox.IsVisible = true;

                    MainFrameBox.IsVisible = false;
                    LoadingDescription.Text = "Loading page content";
                }

                // navigate to requested page
                if (!FrameView.Navigate(page, "", new DrillInNavigationTransitionInfo()))
                {
                    Console.WriteLine("Navigation failed");
                }
                if (clearBackStack)
                    FrameView.BackStack.Clear();
            }
        }
        catch (Exception ex)
        {
            runnerBox.IsVisible = false;
            MainFrameBox.IsVisible = true;

            Console.WriteLine(ex.ToString());

            ShowMessage("Something went wrong", "When loading the page, a error was encountered.\n\n" + ex.ToString());
        }
    }

    private async void Frame_Navigated(object? sender, NavigationEventArgs e)
    {
        // send navigation event
        if (e.Content is SecurityPage page2)
        {
            page2.OnHideLoadingBar += Page_OnHideLoadingBar;
            page2.OnShowLoadingBar += Page_OnShowLoadingBar;
            page2.OnLoadProgress += Page_OnLoadProgress;
            await page2.OnNavigateTo();
        }
        else
        {
            Console.WriteLine("Not a securitypage.");
        }
    }

    private void Frame_NavigationFailed(object? sender, NavigationFailedEventArgs e)
    {
        if (e.Exception != null)
        {
            Console.WriteLine("Navigation failed with " + e.Exception.ToString());
            runnerBox.IsVisible = false;
            MainFrameBox.IsVisible = true;

            ShowMessage("Error", $"Failed to load page in MHSClient:{Environment.NewLine}{e.Exception}");
        }
    }


    private void Page_OnLoadProgress(string progress)
    {
        LoadingDescription.Text = progress;
    }

    private void Page_OnHideLoadingBar(object? sender, EventArgs e)
    {
        runnerBox.IsVisible = false;
        MainFrameBox.IsVisible = true;
    }
    private void Page_OnShowLoadingBar(object? sender, EventArgs e)
    {
        runnerBox.IsVisible = true;
        MainFrameBox.IsVisible = false;
    }

    private async void BtnDownload_Click(object? sender, RoutedEventArgs e)
    {
        Services.SecurityClient.SetHost("https://" + BrowserUtils.GetHost());
        await NavigateTo(typeof(Download));
    }

    // TODO: better way to do this
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(HomePage))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(MusicManager))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(AlarmHistory))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(Weather))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(CfgOverview))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ZonesCfg))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(UsersCfg))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(NotificationsCfg))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(AboutPage))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(MaintenanceOverview))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(FirmwareUpdate))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(EventLog))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(Download))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(LoginPage))]
    private static readonly Dictionary<string, Type> PageNames = new(){
        {"HomePage", typeof(HomePage)},
        {"MusicManager", typeof(MusicManager)},
        {"AlarmHistory", typeof(AlarmHistory)},
        {"Weather", typeof(Weather)},
        {"CfgOverview", typeof(CfgOverview)},
        {"ZonesCfg", typeof(ZonesCfg)},
        {"UsersCfg", typeof(UsersCfg)},
        {"NotificationsCfg", typeof(NotificationsCfg)},
        {"AboutPage", typeof(AboutPage)},
        {"MaintenanceOverview", typeof(MaintenanceOverview)},
        {"FirmwareUpdate", typeof(FirmwareUpdate)},
        {"EventLog", typeof(EventLog)},
        {"Download", typeof(Download)}
    };

    private async void NavigationView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        // send navigate away event to old page
        if (navigationView.Content is SecurityPage page)
        {
            await page.OnNavigateAway();
        }

        // navigate to the selected page

        if (e.InvokedItemContainer is NavigationViewItem nvi)
        {
            if (e.IsSettingsInvoked)
            {
                await NavigateTo(typeof(SettingsPage));
            }
            else
            {
                if (nvi.Tag != null && PageNames.TryGetValue((string)nvi.Tag, out Type? t))
                    await NavigateTo(t);
            }
        }
    }
    private void FrameView_Navigated(object sender, FluentAvalonia.UI.Navigation.NavigationEventArgs e)
    {
        if (e.SourcePageType.Name == "SettingsPage")
        {
            PageTitle.Text = "Settings";
        }
        else
        {

            if (e.Content is Control page)
            {
                var typeName = page.GetType().Name;

                foreach (NavigationViewItem nvi in GetNavigationViewItems(navigationView.MenuItems).Cast<NavigationViewItem>())
                {
                    if (nvi.Tag != null)
                    {
                        if ((string)nvi.Tag == typeName)
                        {
                            navigationView.SelectedItem = nvi;

                            // update page text
                            PageTitle.Text = (string?)nvi.Content;
                        }
                    }
                }
            }
        }
    }

    private static List<object> GetNavigationViewItems(IList<object>? root)
    {
        // not proud of this code
        List<object> items = [];

        if (root == null)
            return items;

        foreach (NavigationViewItem nvi in root.Cast<NavigationViewItem>())
        {
            items.AddRange(GetNavigationViewItems(nvi.MenuItems));
            items.Add(nvi);
        }

        return items;
    }

    internal async Task NavigateToInitialPage()
    {
        Console.WriteLine("NavigateToInitialPage");
        try
        {
            var ip = Services.Preferences.Get("ip");
            var tok = Services.Preferences.Get("user_token");

            if (BrowserUtils.IsBrowser)
            {
                ip = BrowserUtils.GetHost();
            }


            // Try to connect to the server first. If that fails, show login page
            if (!string.IsNullOrEmpty(tok) && !string.IsNullOrEmpty(ip))
            {
                try
                {
                    LoadingDescription.Text = "Connecting to server";
                    // might be a valid token, try to authenticate
                    Services.SecurityClient.SetHost("https://" + ip);
                    if (await Services.SecurityClient.Start(tok) == SecurityApiResult.Success)
                    {
                        LoadingDescription.Text = "Authentication OK";

                        if (Services.SecurityClient.CurrentUser != null && Services.SecurityClient.CurrentUser.Username == "System Installer")
                        {
                            await NavigateTo(typeof(InitialSetup), true);
                        }
                        else
                        {
                            await NavigateTo(typeof(HomePage), true);
                        }
                        return;
                    }
                    else
                    {
                        LoadingDescription.Text = "Authentication FAIL";
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage("Something went wrong", "When connecting, a error was encountered.\n\n" + ex.ToString());
                }
            }

            await NavigateTo(typeof(LoginPage), true);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            ShowMessage("Something went wrong", "When loading the page, a error was encountered.\n\n" + ex.ToString());
        }
    }

    private async void Logout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        FlyoutBase.GetAttachedFlyout(UserPopupButton)?.Hide();
        if (await new ContentDialog()
        {
            Title = "Are you sure?",
            Content = "Are you sure you want to logout? You will no longer be able to access the system or recieve notifications until you log back in.",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close
        }.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                navigationView.IsEnabled = false;

                LoadingDescription.Text = "Connecting to server";
                runnerBox.IsVisible = true;
                MainFrameBox.IsVisible = false;

                var result = await Services.SecurityClient.Logout();
                if (!result.IsSuccess)
                {
                    ShowMessage("System error", "Logout operation failed: " + result.ResultMessage);

                    // show everything again
                    navigationView.IsEnabled = true;
                    runnerBox.IsVisible = false;
                    MainFrameBox.IsVisible = true;
                    return;
                }
                Services.Preferences.Set("user_token", "");
                await NavigateToInitialPage();
            }
            catch
            {
                ShowMessage("System error", "Logout operation failed");
            }

            navigationView.IsEnabled = true;
            runnerBox.IsVisible = false;
            MainFrameBox.IsVisible = true;
        }
    }
    private async void ChangePW_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        FlyoutBase.GetAttachedFlyout(UserPopupButton)?.Hide();
        ChangePasswordView view = new();
        ContentDialog dlg = new()
        {
            PrimaryButtonText = "Change",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Title = "Change password",
            Content = view
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            string oldpw = view.OldPassword;
            string newpw = view.NewPassword1;
            string newpw2 = view.NewPassword2;
            if (string.IsNullOrEmpty(oldpw) || string.IsNullOrEmpty(newpw) || string.IsNullOrEmpty(newpw2))
            {
                await new ContentDialog()
                {
                    Title = "Error",
                    Content = "You must enter a old and new password",
                    CloseButtonText = "Ok"
                }.ShowAsync();

                ChangePW_Click(null, new Avalonia.Interactivity.RoutedEventArgs());
                return;
            }

            if (newpw != newpw2)
            {
                await new ContentDialog()
                {
                    Title = "Error",
                    Content = "The new passwords do not match",
                    CloseButtonText = "Ok"
                }.ShowAsync();

                ChangePW_Click(null, new Avalonia.Interactivity.RoutedEventArgs());
                return;
            }

            if (oldpw == newpw)
            {
                await new ContentDialog()
                {
                    Title = "Error",
                    Content = "The new password must be different from the old password",
                    CloseButtonText = "Ok"
                }.ShowAsync();

                ChangePW_Click(null, new Avalonia.Interactivity.RoutedEventArgs());
                return;
            }

            var result = await Services.SecurityClient.SetCurrentUserPassword(oldpw, newpw);
            if (result.IsSuccess)
            {
                await new ContentDialog()
                {
                    Title = "Success",
                    Content = "Password changed successfully",
                    CloseButtonText = "Ok"
                }.ShowAsync();
            }
            else
            {
                await new ContentDialog()
                {
                    Title = "Error",
                    Content = result.ResultMessage,
                    CloseButtonText = "Ok"
                }.ShowAsync();

                ChangePW_Click(null, new Avalonia.Interactivity.RoutedEventArgs());
                return;
            }
        }
    }
}