using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace e_learning_app
{
    public partial class LoginControl : UserControl
    {
        public LoginControl()
        {
            InitializeComponent();
            this.Loaded += LoginControl_Loaded;
        }
        private void LoginControl_Loaded(object sender, RoutedEventArgs e)
        {
            bool remembered = Properties.Settings.Default.RememberMe;
            chkRememberMe.IsChecked = remembered;

            if (remembered)
            {
                txtEmail.Text = Properties.Settings.Default.SavedEmail;
            }
        }

        private void OpenMainWindow(User user)
        {
            var loginWin = Window.GetWindow(this) as LoginWindow;

            if (user.Role == "Admin")
            {
                var adminWin = new AdminMainWindow();
                adminWin.Show();
                adminWin.Activate();
            }
            else if (user.Role == "Instructor")
            {
                var mainWin = new MainWindow(user);
                mainWin.Show();
                mainWin.Activate();
            }
            else
            {
                var studentWin = new StudentMainWindow(user);
                studentWin.Show();
                studentWin.Activate();
            }

            loginWin?.Close();
        }

        // --- Đăng nhập bằng Google ---------------------------------
        private async void login_google(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            try
            {

                var fbUser = await FirebaseService.LoginWithGoogleAsync();
                if (fbUser != null)
                {
                    string email    = fbUser.Info?.Email ?? "";
                    string fullName = fbUser.Info?.DisplayName ?? email;
                    string uid      = fbUser.Uid;
                    string idToken  = fbUser.Credential.IdToken;

                    // Sync user qua Backend API (đồng bộ thông tin, tạo nếu chưa có)
                    try
                    {
                        await e_learning_app.Class.ApiService.PostAsync("users/sync-user", new
                        {
                            FullName = fullName,
                            Email = email,
                            Provider = "google",
                            PhotoUrl = (string)null
                        });
                    }
                    catch { /* sync user không bắt buộc */ }

                    // Lấy thông tin user qua API
                    var user = new User { Id = uid, Email = email, FullName = fullName, Role = "Student" };
                    try
                    {
                        var userResp = await e_learning_app.Class.ApiService.GetAsync<e_learning_app.Class.UserResponse>($"users/{uid}");
                        if (userResp?.Data != null)
                        {
                            if (!string.IsNullOrWhiteSpace(userResp.Data.FullName))
                                user.FullName = userResp.Data.FullName;
                            user.Role = !string.IsNullOrWhiteSpace(userResp.Data.Role) ? userResp.Data.Role : "Student";
                            if (userResp.Data.IsBlocked)
                            {
                                CustomDialog.Show("Tài khoản của bạn đã bị khóa bởi Admin!", "Thông báo", DialogType.Warning);
                                return;
                            }
                        }
                    }
                    catch { /* không bắt buộc */ }

                    OpenMainWindow(user);
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi dang nhập Google: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        // --- Đăng nhập bằng Email/Password (nút bấm) --------------
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            await DoEmailLogin();
        }

        // --- Đăng nhập bằng Enter ----------------------------------
        private async void login_enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await DoEmailLogin();
        }

        // --- Logic dang nhập email/password dùng chung -------------
        private async Task DoEmailLogin()
        {
            string email    = txtEmail.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                txtstatus.Text = "Vui lòng nhập đầy đủ Email và Mật khẩu!";
                return;
            }
            // -- Admin đặc biệt --
            if (email == "admin" && password == "admin")
            {
                var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6ImFkbWluX3N1cGVyX2lkIiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvZW1haWxhZGRyZXNzIjoiYWRtaW5Ac3lzdGVtLmNvbSIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6IkFkbWluIiwiaXNzIjoiRWR1U21hcnRBUEkiLCJhdWQiOiJFZHVTbWFydFdQRiIsImV4cCI6MjE0NzQ4MzY0N30.Z5tojhXftbkTVsKkfkjcI8g8oqQBGAxTi_2LhMkKxSw";
                e_learning_app.Class.ApiService.SetJwtToken(jwt);
                var loginWin = Window.GetWindow(this) as LoginWindow;
                var adminWin = new AdminMainWindow();
                adminWin.Show();
                adminWin.Activate();
                loginWin?.Close();
                return;
            }

            btnLogin.IsEnabled = false;
            txtstatus.Text = "";

            try
            {
                var loginResult = await FirebaseService.LoginWithTokenAsync(email, password);
                if (loginResult.Uid == null)
                {
                    txtstatus.Text = "Sai Email hoặc Mật khẩu!";
                    return;
                }

                string userId = loginResult.Uid;

                // Sync user qua Backend API (đồng bộ thông tin, tạo nếu chưa có)
                try
                {
                    await e_learning_app.Class.ApiService.PostAsync("users/sync-user", new
                    {
                        FullName = email.Split('@')[0],
                        Email = email,
                        Provider = "email"
                    });
                }
                catch { /* sync user không bắt buộc */ }

                // Lấy thông tin user qua API
                var user = new User { Id = userId, Email = email, FullName = email == "admin" ? "Administrator" : email.Split('@')[0], Role = userId == "admin_super_id" ? "Admin" : "Student" };
                try
                {
                    var userResp = await e_learning_app.Class.ApiService.GetAsync<e_learning_app.Class.UserResponse>($"users/{userId}");
                    if (userResp?.Data != null)
                    {
                        if (!string.IsNullOrWhiteSpace(userResp.Data.FullName))
                            user.FullName = userResp.Data.FullName;
                        user.Role = !string.IsNullOrWhiteSpace(userResp.Data.Role) ? userResp.Data.Role : "Student";
                        if (userResp.Data.IsBlocked)
                        {
                            txtstatus.Text = "Tài khoản của bạn đã bị khóa!";
                            return;
                        }
                    }
                }
                catch {}
                if (chkRememberMe.IsChecked == true)
                {
                    Properties.Settings.Default.SavedEmail = email;
                    Properties.Settings.Default.SavedPassword = password;
                    Properties.Settings.Default.RememberMe = true;
                }
                else
                {
                    Properties.Settings.Default.SavedEmail = "";
                    Properties.Settings.Default.SavedPassword = "";
                    Properties.Settings.Default.RememberMe = false;

                    // Xóa session Firebase để lần sau không tự động dang nhập
                    var repo = new SimpleUserRepository();
                    repo.DeleteUser();
                }
                Properties.Settings.Default.Save();

                OpenMainWindow(user);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi kết nối: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        // --- Quên mật khẩu -----------------------------------------
        private void ForgotPassword_click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new ForgotPasswordControl(email);
            }
        }

        // --- Chuyển sang màn hình dang ký --------------------------
        private void GoToRegister_Click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
                parent.MainContentHolder.Content = new RegisterControl();
        }
    }
}

