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
    /// Interaction logic for LoginControl.xaml
    /// </summary>
    public partial class LoginControl : UserControl
    {
        public LoginControl()
        {
            InitializeComponent();
        }
        private void GoToRegister_Click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new RegisterControl();
            }
        }
        private async void login_google(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            string userId = await FirebaseService.LoginWithGoogleAsync();

            if (userId != null)
            {
                await FirebaseService.CreateUserInFirestore(userId);
                var current_window = Window.GetWindow(this) as LoginWindow;
                StudentMainWindow main = new StudentMainWindow();
                main.Show();
                main.Activate();
                current_window?.Close();
            }

            btnLogin.IsEnabled = true;
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                txtstatus.Text = "Vui lòng nhập đầy đủ Email và Mật khẩu!";
                return;
            }
            btnLogin.IsEnabled = false;

            string userId = await FirebaseService.LoginAsync(email, password);

            if (userId != null)
            {
                var current_window = Window.GetWindow(this) as LoginWindow;
                await FirebaseService.CreateUserInFirestore(userId);
                MainWindow main = new MainWindow();
                main.Show();
                main.Activate();
                current_window?.Close();
            }
            else
            {
                txtstatus.Text = "Sai Email hoặc mật khẩu";
            }

            btnLogin.IsEnabled = true;
        }

        private async void login_enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string email = txtEmail.Text;
                string password = txtPassword.Password;
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    txtstatus.Text = "Vui lòng nhập đầy đủ Email và Mật khẩu!";
                    return;
                }
                btnLogin.IsEnabled = false;

                try
                {
                    string userId = await FirebaseService.LoginAsync(email, password);

                    if (userId != null)
                    {
                        var current_window = Window.GetWindow(this) as LoginWindow;
                        await FirebaseService.CreateUserInFirestore(userId);
                        MainWindow main = new MainWindow();
                        main.Show();
                        main.Activate();
                        current_window?.Close();
                    }
                    else
                    {
                        txtstatus.Text = "Sai Email hoặc mật khẩu";
                    }
                }
                catch (Exception ex) { MessageBox.Show("Lỗi kết nối" + ex.Message); }
                finally
                {
                    btnLogin.IsEnabled = true;
                }
            }
        }

        private void ForgotPassword_click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new ForgotPasswordControl();
            }
        }
    }
}
