using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages;

public partial class NotificationsCfg : SecurityPage
{
    private bool _smtpEnabled;
    public bool SMTPEnabled
    {
        get { return _smtpEnabled; }
        set
        {
            _smtpEnabled = value;

            chkEnableSmtp.IsChecked = value;
            cmbNotifSettings.IsEnabled = value;
            txtSmtpPw.IsEnabled = value;
            txtSmtpRecipients.IsEnabled = value;
            txtSmtpServer.IsEnabled = value;
            txtSmtpUser.IsEnabled = value;
            BtnSendTestEmail.IsEnabled = value;
        }
    }
    public NotificationsCfg()
    {
        InitializeComponent();
        DataContext = this;
        SMTPEnabled = false;
    }

    public override async Task OnNavigateTo()
    {
        UpdateLoadingString("Loading notification settings");
        var res = await Services.SecurityClient.GetNotificationSettings();
        HideLoadingBar();
        if (res.IsSuccess && res.Value != null)
        {
            var cfg = (NotificationSettings)res.Value;
            chkEnableSmtp.IsEnabled = true;

            SMTPEnabled = cfg.smtpEnabled;
            cmbNotifSettings.SelectedIndex = cfg.notificationLevel;
            txtSmtpRecipients.Text = cfg.smtpSendTo;
            txtSmtpUser.Text = cfg.smtpUsername;
            txtSmtpServer.Text = cfg.smtpHost;
        }
        else
        {
            Services.MainView.ShowMessage("Something went wrong", "Failed to fetch notification configuration");
        }
    }

    private async void BtnSave_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var cfg = new NotificationSettings
        {
            notificationLevel = cmbNotifSettings.SelectedIndex,
            smtpEnabled = chkEnableSmtp.IsChecked ?? false,
            smtpHost = txtSmtpServer.Text ?? "",
            smtpSendTo = txtSmtpRecipients.Text ?? "",
            smtpUsername = txtSmtpUser.Text ?? ""
        };

        if (!string.IsNullOrEmpty(txtSmtpPw.Text))
            cfg.smtpPassword = txtSmtpPw.Text;

        UpdateLoadingString("Saving notification settings");
        ShowLoadingBar();
        var resp = await Services.SecurityClient.SaveNotificationSettings(cfg);
        if (resp.IsSuccess)
        {
            Services.MainView.ShowMessage("Notification settings saved", "Settings saved successfully");
        }
        else
        {
            Services.MainView.ShowMessage("Failed to save settings", resp.ResultMessage);
        }
        HideLoadingBar();
    }
    private async void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OnNavigateTo();
    }
    private async void BtnSendTestEmail_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateLoadingString("Connecting to system");
        ShowLoadingBar();
        var resp = await Services.SecurityClient.SendTestEmail();
        HideLoadingBar();

        Services.MainView.ShowMessage("SMTP Operation result", resp.ResultMessage);
    }
}