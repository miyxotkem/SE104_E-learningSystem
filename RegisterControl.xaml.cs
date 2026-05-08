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
                MessageBox.Show("Vui lòng nhập họ và tên!!");
                return;
            }
            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Vui lòng nhập email!!");
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu!!");
                return;
            }
            if (password.Length < 6)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 6 ký tự!!");
                return;
            }
            try
            {
                string newUserId = await FirebaseService.RegisterAsync(email, password);
                this.IsEnabled = false;

                if (newUserId != null)
                {
                    await FirebaseService.CreateUserInFirestore(newUserId, email, fullName); // Lưu vào Firestore với Email và FullName
                    MessageBox.Show("Đăng ký thành công! Bạn có thể đăng nhập ngay bây giờ.");
                    var parent_window=Window.GetWindow(this) as LoginWindow;
                    if (parent_window != null)
                    {
                        parent_window.MainContentHolder.Content =new LoginControl();
                    }
                }
            }
            catch (Exception ex) {
               MessageBox.Show("lỗi đăng ký"+ ex.Message);
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
