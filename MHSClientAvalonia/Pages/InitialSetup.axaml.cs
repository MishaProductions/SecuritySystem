using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MHSClientAvalonia.Utils;

namespace MHSClientAvalonia.Pages;

public partial class InitialSetup : SecurityPage
{
    private readonly IBrush? defaultBrush;
    readonly Control[] Pages = [];

    private string NewPassword1
    {
        get
        {
            if (txtNewPw1.Text == null)
                return "";
            return txtNewPw1.Text;
        }
    }
    private string NewPassword2
    {
        get
        {
            if (txtNewPw2.Text == null)
                return "";
            return txtNewPw2.Text;
        }
    }
    private string Username
    {
        get
        {
            if (txtUsername.Text == null)
                return "";
            return txtUsername.Text;
        }
    }
    public InitialSetup()
    {
        InitializeComponent();
        Pages = [];
        defaultBrush = txtNewPw2.BorderBrush;
    }

    private int pgIndex = 0;

    public override Task OnNavigateTo()
    {
        HideLoadingBar();
        return Task.CompletedTask;
    }
    private void BtnBack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (pgIndex == 1)
        {
            Pg0Welcome.IsVisible = true;
            Pg1UserCfg.IsVisible = false;
            pgIndex--;
            BtnBack.IsEnabled = false;
        }
        else if (pgIndex == 2)
        {
            Pg0Welcome.IsVisible = false;
            Pg1UserCfg.IsVisible = true;
            Pg2Gpio.IsVisible = false;
            pgIndex--;
            BtnBack.IsEnabled = true;
        }
        else if (pgIndex == 3)
        {
            Pg0Welcome.IsVisible = false;
            Pg1UserCfg.IsVisible = false;
            Pg2Gpio.IsVisible = true;
            Pg3Completion.IsVisible = false;
            pgIndex--;
            BtnBack.IsEnabled = true;
            BtnNext.Content = "Next";
        }
    }
    private async void BtnNext_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (pgIndex == 0)
        {
            Pg0Welcome.IsVisible = false;
            Pg1UserCfg.IsVisible = true;
            pgIndex++;

            BtnBack.IsEnabled = true;
        }
        else if (pgIndex == 1)
        {
            if (NewPassword1 != NewPassword2)
            {
                txtNewPw1.BorderBrush = Brushes.Red;
                txtNewPw2.BorderBrush = Brushes.Red;

                lbl3.Content = "Passwords do not match";
                lbl3.IsVisible = true;
                return;
            }
            if (string.IsNullOrEmpty(NewPassword1))
            {
                txtNewPw1.BorderBrush = Brushes.Red;
                txtNewPw2.BorderBrush = Brushes.Red;

                lbl3.Content = "Password cannot be empty";
                lbl3.IsVisible = true;
                return;
            }
            if (string.IsNullOrEmpty(Username))
            {
                txtUsername.BorderBrush = Brushes.Red;

                lbl3.Content = "Username cannot be empty";
                lbl3.IsVisible = true;
                return;
            }
            if (Username == "System Installer")
            {
                txtUsername.BorderBrush = Brushes.Red;

                lbl3.Content = "The username is reserved by the system";
                lbl3.IsVisible = true;
                return;
            }

            txtUsername.BorderBrush = defaultBrush;
            txtNewPw1.BorderBrush = defaultBrush;
            txtNewPw2.BorderBrush = defaultBrush;
            lbl3.IsVisible = false;



            Pg0Welcome.IsVisible = false;
            Pg1UserCfg.IsVisible = false;
            Pg2Gpio.IsVisible = true;
            pgIndex++;
        }
        else if (pgIndex == 2)
        {
            // Driver config page complete

            Pg0Welcome.IsVisible = false;
            Pg1UserCfg.IsVisible = false;
            Pg2Gpio.IsVisible = false;
            Pg3Completion.IsVisible = true;
            BtnNext.Content = "Finish";
            pgIndex++;
        }

        else if (pgIndex == 3)
        {
            UpdateLoadingString("Saving changes");
            ShowLoadingBar();

            var res = await Services.SecurityClient.CompleteSystemOobe(new MHSApi.API.SaveSysConfig(Username, NewPassword1, CmbGpioDriver.SelectedIndex == 1));
            if (res.IsFailure)
            {
                Services.MainView.ShowMessage("Failed to update system settings", res.ResultMessage);
            }
            else
            {
                await Services.MainView.NavigateToInitialPage();
            }
        }
    }

    private void TextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (NewPassword1 != NewPassword2)
        {
            txtNewPw1.BorderBrush = Brushes.Red;
            txtNewPw2.BorderBrush = Brushes.Red;

            lbl3.Content = "Passwords do not match";
            lbl3.IsVisible = true;
        }
        else
        {
            txtNewPw1.BorderBrush = defaultBrush;
            txtNewPw2.BorderBrush = defaultBrush;

            lbl3.IsVisible = false;
        }
    }

    private void Username_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrWhiteSpace(Username))
        {
            txtUsername.BorderBrush = Brushes.Red;

            lbl3.Content = "Username cannot be empty or whitespace";
            lbl3.IsVisible = true;
        }
        else
        {
            txtUsername.BorderBrush = defaultBrush;

            lbl3.IsVisible = false;
        }
    }
}