using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using SecuritySystemApi;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace MHSClientAvalonia.Pages
{
    public partial class LoginPage : SecurityPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        public override async Task OnNavigateTo()
        {
            await base.OnNavigateTo();
            await StartLoginProcess();
        }

        
        public async Task StartLoginProcess()
        {
            // Determine if we need to show the login page.
            AutoLoginBar.IsOpen = false;
            runner.IsVisible = true;
            LoginBox.IsVisible = false;

            var ip = Services.Preferences.Get("ip");
            var tok = Services.Preferences.Get("user_token");

            if (BrowserUtils.IsBrowser)
            {
                ip = BrowserUtils.GetHost();
                txtIp.IsVisible = false;

                lblIp.IsVisible = false;

            }
            if (!string.IsNullOrEmpty(tok) && !string.IsNullOrEmpty(ip))
            {
                try
                {
                    LoadingDescription.Text = "Connecting to server";
                    // might be a valid token, try to authenticate
                    Services.SecurityClient.SetHost("https://" + ip);

                    Console.WriteLine("StartLoginProcess - Starting now");

                    var res = await Services.SecurityClient.Start(tok);
                    if (res == SecurityApiResult.Success)
                    {
                        LoadingDescription.Text = "Authentication OK";
                        await Services.MainView.NavigateTo(typeof(HomePage), true);
                        return;
                    }
                    else if (res == SecurityApiResult.IncorrectUsernameOrPassword)
                    {
                        LoadingDescription.Text = "Authentication FAIL";
                        BarWrongPassword.IsOpen = true;
                    }
                    else if (res == SecurityApiResult.MissingInvaildAuthToken)
                    {
                        LoadingDescription.Text = "Authentication FAIL";
                        BarSessionExpire.IsOpen = true;
                    }
                    else
                    {
                        LoadingDescription.Text = "Authentication FAIL";
                        AutoLoginBar.IsOpen = true;
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Open Autobar");
                    // Something went wrong while signing in.
                    AutoLoginBar.IsOpen = true;

                    await new ContentDialog()
                    {
                        Title = "Error",
                        Content = "The following error has occured while attempting to login using the cached login token:\n" + ex.ToString(),
                        PrimaryButtonText = "OK"
                    }.ShowAsync();
                }
            }

            // We couldn't find the token or IP, or it was invaild. Show the login page.
            runner.IsVisible = false;
            LoginBox.IsVisible = true;
            txtIp.Text = ip;
        }

        private async void BtnRetryLogin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await StartLoginProcess();
        }
        private async void Login_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await DoLogin();
        }

        private async Task DoLogin()
        {
            AutoLoginBar.IsOpen = false;
            BarHostError.IsOpen = false;
            BarWrongPassword.IsOpen = false;
            BarInternalError.IsOpen = false;
            var ip = txtIp.Text;

            if (ip == null)
            {
                await new ContentDialog()
                {
                    Title = "Alert",
                    Content = "Please enter a valid IP adddress",
                    PrimaryButtonText = "OK"
                }.ShowAsync();

                return;
            }

            // show loading screen
            LoginBox.IsVisible = false;
            runner.IsVisible = true;

            Services.SecurityClient.SetHost("https://" + ip);

            LoadingDescription.Text = "Connecting to server";
            try
            {
                var data = await Services.SecurityClient.FetchUpdateData();

                if (data == null)
                {
                    LoginBox.IsVisible = true;
                    runner.IsVisible = false;
                    AutoLoginBar.IsOpen = true;

                    await new ContentDialog()
                    {
                        Title = "Alert",
                        Content = "Failed to verify if the MHS system is MHS",
                        PrimaryButtonText = "OK"
                    }.ShowAsync();

                    return;
                }
            }
            catch
            {
                LoginBox.IsVisible = true;
                runner.IsVisible = false;
                BarHostError.IsOpen = true;
                return;
            }
            LoadingDescription.Text = "Authenticating";
            try
            {
                // Validate username/password
                var data = await Services.SecurityClient.Login(txtUser.Text ?? "", txtPw.Text ?? "");
                if (data.Item1 == SecurityApiResult.Success)
                {
                    LoadingDescription.Text = "Authentication OK";
                    txtUser.Text = "";
                    txtPw.Text = "";
                    var wsResult = await Services.SecurityClient.Start(data.Item2);
                    if (wsResult == SecurityApiResult.Success)
                    {
                        Services.Preferences.Set("user_token", data.Item2);
                        await Services.MainView.NavigateTo(typeof(HomePage), true);
                    }
                    else
                    {
                        // show login box
                        LoginBox.IsVisible = true;
                        runner.IsVisible = false;

                        await new ContentDialog()
                        {
                            Title = "Error",
                            Content = "The following error has occured while connecting to the websocket:\n" + wsResult,
                            PrimaryButtonText = "OK"
                        }.ShowAsync();
                    }
                }
                else if (data.Item1 == SecurityApiResult.IncorrectUsernameOrPassword)
                {
                    LoginBox.IsVisible = true;
                    runner.IsVisible = false;
                    BarWrongPassword.IsOpen = true;
                }
                else
                {
                    LoginBox.IsVisible = true;
                    runner.IsVisible = false;
                    BarInternalError.IsOpen = true;

                    await new ContentDialog()
                        {
                            Title = "Error",
                            Content = "The following internal error has occured while connecting to the device:\n" + data.Item2,
                            PrimaryButtonText = "OK"
                        }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                // show login box
                LoginBox.IsVisible = true;
                runner.IsVisible = false;
                await new ContentDialog()
                {
                    Title = "Alert",
                    Content = "The following error has occured while authenticating:\n" + ex.ToString(),
                    PrimaryButtonText = "OK"
                }.ShowAsync();
                return;
            }

            Services.Preferences.Set("ip", ip);
        }
    }
}