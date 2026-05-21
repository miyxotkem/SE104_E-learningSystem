
using e_learning_app.Views;
using Google.Apis.Util.Store;
using System;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseManager _dbManager;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private bool _isForceLogout = false;
        public MainWindow(User loggedInUser)
        {
            InitializeComponent();

            _dbManager = new DatabaseManager();

            if (loggedInUser != null)
            {
                _dbManager.SetCurrentUser(loggedInUser);
                this.DataContext = loggedInUser;
            }

            btnDashBoard.Focus();

            this.Loaded += MainWindow_Loaded;
            this.Closed += (s, e) => _statusTimer?.Stop();

            StartUserStatusListener(loggedInUser?.Id);
        }

        private void StartUserStatusListener(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(15);
            _statusTimer.Tick += async (s, e) =>
            {
                try
                {
                    var userResponse = await e_learning_app.Class.ApiService.GetAsync<e_learning_app.Class.UserResponse>("users/profile");
                    if (userResponse != null && userResponse.Data != null && userResponse.Data.IsBlocked)
                    {
                        _statusTimer?.Stop();
                        CustomDialog.Show("Tài khoản của bạn đã bị Admin khóa. Bạn sẽ bị đăng xuất ngay lập tức.", "Cảnh báo bảo mật", DialogType.Error);

                        FirebaseService.SignOut();

                        var loginWin = new LoginWindow(true);
                        loginWin.Show();
                        _isForceLogout = true;
                        this.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi kiểm tra trạng thái user: " + ex.Message);
                }
            };
            _statusTimer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var user = _dbManager.GetCurrentUser();
            if (user == null)
            {
                CustomDialog.Show("Không thể xác định thông tin người dùng. Vui lòng đăng nhập lại.", "Lỗi", DialogType.Error);
                BtnLogout_Click(null, null);
                return;
            }

            if (user.Role == "Instructor")
                MainContentArea.Content = new TeacherDashboardView(_dbManager);
            else
                MainContentArea.Content = new StudentDashboardView(_dbManager);
        }

        // ─── Navigation Logic ─────────────────────────────────────────

        public void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager.GetCurrentUser()?.Role == "Instructor")
                MainContentArea.Content = new TeacherDashboardView(_dbManager);
            else
                MainContentArea.Content = new StudentDashboardView(_dbManager);
        }

        private void NavSchedule_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new TeachingScheduleView(_dbManager);
        }

        public void NavClasses_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new MyClassesView(_dbManager);
        }


        public void NavExams_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ExamManagementView(_dbManager);
        }


        public void NavNotifications_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new NotificationsView(_dbManager);
        }



        public void NavigateTo(UserControl view) => MainContentArea.Content = view;

        public void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ProfileManage(_dbManager);
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = CustomDialog.Confirm(
                        "Bạn có chắc muốn đăng xuất?",
                        "Xác nhận đăng xuất",
                        "Đăng xuất", "Hủy",
                        DialogType.Question);

            if (confirmed)
            {
                // Xóa cache Google nếu có
                string credPath = "gg.auth.api";
                var dataStore = new FileDataStore(credPath, true);
                await dataStore.ClearAsync();

                // Đăng xuất Firebase (Xóa Token session)
                FirebaseService.SignOut();

                var loginWin = new LoginWindow(true);
                loginWin.Show();
                _isForceLogout = true;
                this.Close();
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Nếu đang bị force close (bị admin khóa) thì không hỏi
            if (_isForceLogout)
            {
                base.OnClosing(e);
                return;
            }

            var result = CustomDialog.ShowExit("Bạn muốn làm gì trước khi thoát?", "Xác nhận");
            if (result == CustomDialogResult.Cancel)
            {
                e.Cancel = true; // Ở lại app
                return;
            }

            if (result == CustomDialogResult.Logout)
            {
                // Đăng xuất: xóa token
                string credPath = "gg.auth.api";
                var dataStore = new FileDataStore(credPath, true);
                dataStore.ClearAsync().Wait();
                FirebaseService.SignOut();
            }
            base.OnClosing(e);
        }
    }
}

