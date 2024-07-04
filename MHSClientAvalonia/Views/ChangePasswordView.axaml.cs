using Avalonia.Controls;
using Avalonia.Media;

namespace MHSClientAvalonia.Views
{
    public partial class ChangePasswordView : UserControl
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
        public string OldPassword
        {
            get
            {
                if (txtOldPw.Text == null)
                    return "";
                return txtOldPw.Text;
            }
        }
        public bool OldPasswordRequired
        {
            get
            {
                return txtOldPw.IsVisible; 
            }
            set
            {
                txtOldPw.IsVisible = value;
                lblOldPw.IsVisible = value;
            }
        }
        private IBrush? defaultBrush;
        public ChangePasswordView()
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

                lbl3.IsVisible = true;
            }
            else
            {
                txtNewPw1.BorderBrush = defaultBrush;
                txtNewPw2.BorderBrush = defaultBrush;

                lbl3.IsVisible = false;
            }
        }
    }
}
