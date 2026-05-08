using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace e_learning_app
{
    public partial class LoginControl : UserControl
    {
        // Email duy nhất có role giáo viên
        private const string TeacherEmail = "buitrantrongnguyen@gmail.com";

        public LoginControl()
        {
            InitializeComponent();
        }

        // ─── Helper: xác định role theo email ───────────────────────
        private static string GetRoleByEmail(string email)
        {
            return string.Equals(email?.Trim(), TeacherEmail, StringComparison.OrdinalIgnoreCase)
                ? "Instructor"
                : "Student";
        }

        // ─── Helper: mở cửa sổ chính với user đã xác thực ───────────
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
                        FullName = fullName,
                        Role     = GetRoleByEmail(email)
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
                            }
                        }
                    }
                    catch { /* không bắt buộc */ }

                    OpenMainWindow(user);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đăng nhập Google: " + ex.Message);
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
                    FullName = email.Split('@')[0],
                    Role     = GetRoleByEmail(email)
                };

                // Fetch đúng Firestore document ID (quan trọng: phải khớp với InstructorId trong courses)
                try
                {
                    if (FirebaseService.Db != null)
                    {
                        // Tìm theo Email để lấy Firestore document ID thực sự
                        var query = await FirebaseService.Db.Collection("Users")
                            .WhereEqualTo("Email", email)
                            .Limit(1)
                            .GetSnapshotAsync();

                        if (query.Count > 0)
                        {
                            var stored = query.Documents[0].ConvertTo<User>();
                            // stored.Id = Firestore document ID thực (khớp với InstructorId trong courses)
                            if (!string.IsNullOrWhiteSpace(stored?.Id))
                                user.Id = stored.Id;
                            if (!string.IsNullOrWhiteSpace(stored?.FullName))
                                user.FullName = stored.FullName;
                            if (!string.IsNullOrWhiteSpace(stored?.Role))
                                user.Role = stored.Role;
                        }
                    }
                }
                catch { /* không bắt buộc */ }

                OpenMainWindow(user);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message);
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        // ─── Quên mật khẩu ─────────────────────────────────────────
        private async void ForgotPassword_click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string email = txtEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                txtstatus.Text = "Vui lòng nhập Email để đặt lại mật khẩu!";
                return;
            }
            bool ok = await FirebaseService.SendPasswordResetAsync(email);
            if (ok)
                MessageBox.Show("Email đặt lại mật khẩu đã được gửi!", "Thành công");
            else
                MessageBox.Show("Không thể gửi email. Vui lòng kiểm tra lại địa chỉ email.", "Lỗi");
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
