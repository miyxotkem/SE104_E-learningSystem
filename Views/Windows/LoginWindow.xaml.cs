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
using System.Windows.Shapes;

namespace e_learning_app
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow() : this(skipAutoLogin: false) { }
        public LoginWindow(bool skipAutoLogin = false)
        {

            InitializeComponent();
            if (skipAutoLogin)
            {
                MainContentHolder.Content = new LoginControl();
                MainContentHolder.Visibility = Visibility.Visible;
            }
            else
            {
                CheckAutoLoginAsync();
            }
        }

        private async void CheckAutoLoginAsync()
        {
            MainContentHolder.Visibility = Visibility.Collapsed;

            try
            {
                await Task.Delay(500);

                var currentUser = FirebaseService.Auth?.User;

                if (currentUser != null)
                {
                    string savedJwt = e_learning_app.Class.SecureTokenManager.GetToken();
                    if (!string.IsNullOrEmpty(savedJwt))
                    {
                        e_learning_app.Class.ApiService.SetJwtToken(savedJwt);
                    }
                    else
                    {
                        MainContentHolder.Content = new LoginControl();
                        MainContentHolder.Visibility = Visibility.Visible;
                        return;
                    }

                    // Lấy profile user qua Backend API (không còn dùng Firestore trực tiếp)
                    var userResp = await e_learning_app.Class.ApiService.GetAsync<e_learning_app.Class.UserResponse>($"users/{currentUser.Uid}");

                    if (userResp?.Data != null)
                    {
                        var user = userResp.Data;
                        user.Id = currentUser.Uid;

                        if (!user.IsBlocked)
                        {
                            if (user.Role == "Instructor")
                                new MainWindow(user).Show();
                            else
                                new StudentMainWindow(user).Show();

                            this.Close();
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AutoLogin error: " + ex.Message);
            }

            MainContentHolder.Content = new LoginControl();
            MainContentHolder.Visibility = Visibility.Visible;
        }
        private void btnclose(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }
}
