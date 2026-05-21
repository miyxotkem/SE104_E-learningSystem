using e_learning_app.Views;
using Google.Apis.Util.Store;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class StudentMainWindow : Window
    {
        private readonly DatabaseManager _dbManager;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private bool _isForceLogout = false;
        public StudentMainWindow(User loggedInUser)
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
            _dbManager.Initialize();

            if (loggedInUser != null)
            {
                _dbManager.SetCurrentUser(loggedInUser);
                this.DataContext = loggedInUser;
            }

            this.Loaded += StudentMainWindow_Loaded;
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

        private void StudentMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hiển thị chữ viết tắt của tên trong avatar
            var user = _dbManager.GetCurrentUser();
            if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                var parts = user.FullName.Trim().Split(' ');
                string initials = parts.Length >= 2
                    ? $"{parts[0][0]}{parts[^1][0]}"
                    : user.FullName.Substring(0, System.Math.Min(2, user.FullName.Length));
                //TxtAvatarInitials.Text = initials.ToUpper();
            }

            // Load trang Dashboard mặc định
            StudentContentArea.Content = new StudentDashboardView(_dbManager);
            SetActiveNav(BtnDashboard);
        }

        private void SetActiveNav(Button activeBtn)
        {
            BtnDashboard.Style      = (Style)FindResource("StudentNavBtn");
            BtnCourses.Style        = (Style)FindResource("StudentNavBtn");
            BtnSchedule.Style       = (Style)FindResource("StudentNavBtn");
            BtnQuiz.Style           = (Style)FindResource("StudentNavBtn");
            BtnNotifications.Style  = (Style)FindResource("StudentNavBtn");

            activeBtn.Style = (Style)FindResource("StudentNavBtnActive");
        }

        public void NavigateTo(UserControl view)
        {
            StudentContentArea.Content = view;
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnDashboard);
            NavigateTo(new StudentDashboardView(_dbManager));
        }

        private void BtnCourses_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnCourses);
            NavigateTo(new MyClassesView(_dbManager));
        }

        private void BtnSchedule_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnSchedule);
            NavigateTo(new TeachingScheduleView(_dbManager));
        }

        public void BtnQuiz_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnQuiz);
            NavigateTo(new StudentQuizView(_dbManager));
        }

        public void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnNotifications);
            StudentContentArea.Content = new NotificationsView(_dbManager);
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

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new ProfileManage(_dbManager));
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
            // Exit = thoát nhưng giữ token → lần sau mở lên tự login

            base.OnClosing(e);
        }
    }
}
