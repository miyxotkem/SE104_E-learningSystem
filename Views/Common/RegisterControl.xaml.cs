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
    /// Interaction logic for RegisterControl.xaml
    /// </summary>
    public partial class RegisterControl : UserControl
    {
        public RegisterControl()
        {
            InitializeComponent();
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                CustomDialog.Show("Vui lòng nhập họ và tên!!", "Thông báo", DialogType.Warning);
                return;
            }
            if (string.IsNullOrEmpty(email))
            {
                CustomDialog.Show("Vui lòng nhập email!!", "Thông báo", DialogType.Warning);
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                CustomDialog.Show("Vui lòng nhập mật khẩu!!", "Thông báo", DialogType.Warning);
                return;
            }
            if (password.Length < 6)
            {
                CustomDialog.Show("Mật khẩu phải có ít nhất 6 ký tự!!", "Thông báo", DialogType.Warning);
                return;
            }
            try
            {
                string newUserId = await FirebaseService.RegisterAsync(email, password, fullName);
                this.IsEnabled = false;

                if (newUserId != null)
                {
                    CustomDialog.Show("Đăng ký thành công! Bạn có thể dang nhập ngay bây giờ.", "Thành công", DialogType.Success);
                    var parent_window=Window.GetWindow(this) as LoginWindow;
                    if (parent_window != null)
                    {
                        parent_window.MainContentHolder.Content =new LoginControl();
                    }
                }
            }
            catch (Exception ex) {
                CustomDialog.Show("Lỗi dang ký: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }

        }

        private void GoToLogin_Click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new LoginControl();
            }
        }
    }
}
