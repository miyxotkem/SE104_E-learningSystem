using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace e_learning_app
{
    /// <summary>
    /// Interaction logic for ForgotPasswordControl.xaml
    /// </summary>
    public partial class ForgotPasswordControl : UserControl
    {
        public ForgotPasswordControl(string email = "")
        {
            InitializeComponent();
            txtResetEmail.Text = email;
        }

        private async void btnSendRequest_Click(object sender, RoutedEventArgs e)
        {
            string email = txtResetEmail.Text;
            if (string.IsNullOrEmpty(email))
            {
                txtStatus.Text = "Vui lòng nhập email!!";
                txtStatus.Foreground = Brushes.Red;
                return;
            }
            btnSendRequest.IsEnabled = false;
            bool issent= await FirebaseService.SendPasswordResetAsync(email);

            if (issent)
            {
                CustomDialog.Show("Hệ thống đã gửi link khôi phục vào Email của bạn. Hãy kiểm tra nhé!", "Thành công", DialogType.Success);
                var parent = Window.GetWindow(this) as LoginWindow;
                if (parent != null)
                {
                    parent.MainContentHolder.Content = new LoginControl();
                }
            }
            else
            {
                txtStatus.Text = "Email không tồn tại hoặc có lỗi xảy ra.";
                txtStatus.Foreground = Brushes.Red;
                btnSendRequest.IsEnabled = true;
            }

        }

        private void BackToLogin_Click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new LoginControl();
            }
        }
    }
}
