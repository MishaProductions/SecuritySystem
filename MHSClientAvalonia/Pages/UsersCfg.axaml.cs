using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using MHSApi.API;
using MHSClientAvalonia.Utils;
using MHSClientAvalonia.Views;

namespace MHSClientAvalonia.Pages;

public partial class UsersCfg : SecurityPage
{
    public UsersCfg()
    {
        InitializeComponent();
    }

    public override async void OnNavigateTo()
    {
        base.OnNavigateTo();
        ShowLoadingBar();

        var retr = await Services.SecurityClient.GetAllUsers();
        if (retr.IsSuccess && retr.Value != null)
        {
            TargetDataGrid.ItemsSource = (ApiUser[]?)retr.Value;
        }
        else
        {
            Services.MainView.ShowMessage("Something went wrong", "Failed to get list of users from the server: " + retr.ResultMessage);
        }

        HideLoadingBar();
    }

    private void Reload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OnNavigateTo();
    }

    private async void BtnNewUser_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NewUserView newUserView = new();
        ContentDialog dlg = new()
        {
            Content = newUserView,
            Title = "Create a new user",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Create",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrEmpty(newUserView.Username) || string.IsNullOrWhiteSpace(newUserView.Username))
            {
                Services.MainView.ShowMessage("Invalid username", "Username cannot be empty");
                return;
            }

            if (string.IsNullOrEmpty(newUserView.NewPassword1) || string.IsNullOrWhiteSpace(newUserView.NewPassword1))
            {
                Services.MainView.ShowMessage("Invalid password", "Password cannot be empty");
                return;
            }

            if (newUserView.NewPassword1 != newUserView.NewPassword2)
            {
                Services.MainView.ShowMessage("Invalid password", "Requested passwords do not match");
                return;
            }

            UpdateLoadingString("Communicating with server");
            ShowLoadingBar();
            var resp = await Services.SecurityClient.CreateUser(newUserView.Username, newUserView.NewPassword1, (UserPermissions)(newUserView.cmbPermissions.SelectedIndex + 1));
            HideLoadingBar();

            if (resp.IsSuccess)
            {
                OnNavigateTo();
            }
            else
            {
                Services.MainView.ShowMessage("Failed to create user", resp.ResultMessage);
            }
        }
    }
    private async void BtnDeleteUser_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TargetDataGrid.SelectedItem == null) return;
        ApiUser user = (ApiUser)TargetDataGrid.SelectedItem;

        ContentDialog dlg = new()
        {
            Content = "Are you sure you want to delete user account " + user.Username + " ?",
            Title = "Delete user",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "DELETE",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            UpdateLoadingString("Communicating with server");
            ShowLoadingBar();
            var resp = await Services.SecurityClient.DelUser(user.ID.ToString());
            HideLoadingBar();

            if (resp.IsSuccess)
            {
                OnNavigateTo();
            }
            else
            {
                Services.MainView.ShowMessage("Failed to delete user", resp.ResultMessage);
            }
        }
    }
    private async void BtnChangePerms_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TargetDataGrid.SelectedItem == null) return;
        ApiUser user = (ApiUser)TargetDataGrid.SelectedItem;

        ChangePermissionView changePermsView = new();
        changePermsView.cmbPermissions.SelectedIndex = (int)user.Permissions - 1;

        ContentDialog dlg = new()
        {
            Content = changePermsView,
            Title = $"Change permission for account {user.Username}",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Update",
            DefaultButton = ContentDialogButton.Primary
        };


        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            UpdateLoadingString("Connecting to system");
            ShowLoadingBar();
            var resp = await Services.SecurityClient.ModUserPermission(user.ID.ToString(), (UserPermissions)(changePermsView.cmbPermissions.SelectedIndex + 1));
            HideLoadingBar();

            if (resp.IsSuccess)
            {
                OnNavigateTo();
            }
            else
            {
                Services.MainView.ShowMessage("Failed to change permission", resp.ResultMessage);
            }
        }
    }
    private async void BtnResetPass_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TargetDataGrid.SelectedItem == null) return;
        ApiUser user = (ApiUser)TargetDataGrid.SelectedItem;

        ChangePasswordView newUserView = new() { OldPasswordRequired = false };
        ContentDialog dlg = new()
        {
            Content = newUserView,
            Title = $"Change password for account {user.Username}",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Update",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrEmpty(newUserView.NewPassword1) || string.IsNullOrWhiteSpace(newUserView.NewPassword1))
            {
                Services.MainView.ShowMessage("Invalid password", "Password cannot be empty");
                return;
            }
            if (newUserView.NewPassword1 != newUserView.NewPassword2)
            {
                Services.MainView.ShowMessage("Invalid password", "Passwords do not match");
                return;
            }

            UpdateLoadingString("Connecting to system");
            ShowLoadingBar();
            var resp = await Services.SecurityClient.ModUserPw(user.ID.ToString(), newUserView.NewPassword1);
            HideLoadingBar();

            if (resp.IsSuccess)
            {
                OnNavigateTo();
            }
            else
            {
                Services.MainView.ShowMessage("Failed to change password", resp.ResultMessage);
            }
        }
    }
}