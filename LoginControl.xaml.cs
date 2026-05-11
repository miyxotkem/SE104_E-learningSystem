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

            if (user.Role == "Instructor")
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

        // ─── Đăng nhập bằng Google ─────────────────────────────────
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


                    await FirebaseService.CreateUserInFirestore(uid, email, fullName);

                    var user = new User
                    {
                        Id       = uid,
                        Email    = email,
                        FullName = fullName
                    };

                    // Cố lấy FullName thực từ Firestore nếu có
                    try
                    {
                        if (FirebaseService.Db != null)
                        {
                            var doc = await FirebaseService.Db.Collection("Users").Document(uid).GetSnapshotAsync();
                            if (doc.Exists)
                            {
                                var stored = doc.ConvertTo<User>();
                                if (!string.IsNullOrWhiteSpace(stored?.FullName))
                                    user.FullName = stored.FullName;
                                if (!string.IsNullOrWhiteSpace(stored?.Role))
                                    user.Role = stored.Role;
                                else
                                    user.Role = "Student";

                                if (stored != null && stored.IsBlocked)
                                {
                                    CustomDialog.Show("Tài khoản của bạn đã bị khóa bởi Admin!", "Thông báo", DialogType.Warning);
                                    return;
                                }
                            }
                        }
                    }
                    catch { /* không bắt buộc */ }

                    OpenMainWindow(user);
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi đăng nhập Google: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        // ─── Đăng nhập bằng Email/Password (nút bấm) ──────────────
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            await DoEmailLogin();
        }

        // ─── Đăng nhập bằng Enter ──────────────────────────────────
        private async void login_enter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await DoEmailLogin();
        }

        // ─── Logic đăng nhập email/password dùng chung ─────────────
        private async Task DoEmailLogin()
        {
            string email    = txtEmail.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                txtstatus.Text = "Vui lòng nhập đầy đủ Email và Mật khẩu!";
                return;
            }

            // ── Admin đặc biệt ──
            if (email == "admin" && password == "admin")
            {
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
                string userId = await FirebaseService.LoginAsync(email, password);

                if (userId == null)
                {
                    txtstatus.Text = "Sai Email hoặc Mật khẩu!";
                    return;
                }

                // Đảm bảo document tồn tại trên Firestore
                await FirebaseService.CreateUserInFirestore(userId, email);

                // Xây dựng user object ban đầu từ Firebase Auth UID
                var user = new User
                {
                    Id       = userId,
                    Email    = email,
                    FullName = email.Split('@')[0]
                };

                // Fetch đúng Firestore document (dùng userId làm Document ID)
                try
                {
                    if (FirebaseService.Db != null)
                    {
                        var doc = await FirebaseService.Db.Collection("Users").Document(userId).GetSnapshotAsync();
                        if (doc.Exists)
                        {
                            var stored = doc.ConvertTo<User>();
                            if (!string.IsNullOrWhiteSpace(stored?.FullName))
                                user.FullName = stored.FullName;
                            if (!string.IsNullOrWhiteSpace(stored?.Role))
                                user.Role = stored.Role;
                            else
                                user.Role = "Student";

                            if (stored != null && stored.IsBlocked)
                            {
                                txtstatus.Text = "Tài khoản của bạn đã bị khóa!";
                                return;
                            }
                        }
                        else
                        {
                            user.Role = "Student";
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

                    // Xóa session Firebase để lần sau không tự động đăng nhập
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

        // ─── Quên mật khẩu ─────────────────────────────────────────
        private void ForgotPassword_click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
            {
                parent.MainContentHolder.Content = new ForgotPasswordControl(email);
            }
        }

        // ─── Chuyển sang màn hình đăng ký ──────────────────────────
        private void GoToRegister_Click(object sender, MouseButtonEventArgs e)
        {
            var parent = Window.GetWindow(this) as LoginWindow;
            if (parent != null)
                parent.MainContentHolder.Content = new RegisterControl();
        }
    }
}
