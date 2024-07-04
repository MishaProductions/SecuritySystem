using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MHSClientAvalonia;

public partial class NewUserView : UserControl
{
    public string NewPassword1
    {
        get
        {
            if (txtNewPw1.Text == null)
                return "";
            return txtNewPw1.Text;
        }
    }
    public string NewPassword2
    {
        get
        {
            if (txtNewPw2.Text == null)
                return "";
            return txtNewPw2.Text;
        }
    }
    public string Username
    {
        get
        {
            if (txtUsername.Text == null)
                return "";
            return txtUsername.Text;
        }
    }

    private IBrush? defaultBrush;
    public NewUserView()
    {
        InitializeComponent();
        defaultBrush = txtNewPw2.BorderBrush;
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