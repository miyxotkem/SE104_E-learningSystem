
using e_learning_app.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using Google.Cloud.Firestore;

namespace e_learning_app
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseManager _dbManager;
        private FirestoreChangeListener _userListener;

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
            this.Closed += (s, e) => _userListener?.StopAsync();

            StartUserStatusListener(loggedInUser?.Id);
        }

        private void StartUserStatusListener(string userId)
        {
            if (string.IsNullOrEmpty(userId) || FirebaseService.Db == null) return;

            DocumentReference docRef = FirebaseService.Db.Collection("Users").Document(userId);
            _userListener = docRef.Listen(snapshot =>
            {
                if (snapshot.Exists)
                {
                    var user = snapshot.ConvertTo<User>();
                    if (user != null && user.IsBlocked)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _userListener?.StopAsync();
                            MessageBox.Show("Tài khoản của bạn đã bị Admin khóa. Bạn sẽ bị đăng xuất ngay lập tức.", "Cảnh báo bảo mật", MessageBoxButton.OK, MessageBoxImage.Stop);

                            var loginWin = new LoginWindow();
                            loginWin.Show();
                            this.Close();
                        });
                    }
                }
            });
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var user = _dbManager.GetCurrentUser();
            if (user == null)
            {
                MessageBox.Show("Không thể xác định thông tin người dùng. Vui lòng đăng nhập lại.", "Lỗi");
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

        public void NavQuestions_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new QuestionBankView();
        }

        public void NavExams_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ExamManagementView(_dbManager);
        }

        public void NavReports_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ReportsView(_dbManager);
        }

        public void NavNotifications_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new NotificationsView();
        }

        public void NavSemestersettings_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new SemesterSettingsView();
        }

        public void NavigateTo(UserControl view) => MainContentArea.Content = view;

        public void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ProfileManage(_dbManager);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                        "Bạn có chắc muốn đăng xuất?",
                        "Xác nhận đăng xuất",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var loginWin = new LoginWindow();
                loginWin.Show();
                this.Close();
            }
        }
    }
}

