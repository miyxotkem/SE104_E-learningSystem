using System.Windows;
using System.Windows.Controls;
using e_learning_app.Views;

namespace e_learning_app
{
    public partial class StudentMainWindow : Window
    {
        private readonly DatabaseManager _dbManager;

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
                TxtAvatarInitials.Text = initials.ToUpper();
            }

            // Load trang Dashboard mặc định
            StudentContentArea.Content = new StudentDashboardView(_dbManager);
            SetActiveNav(BtnDashboard);
        }

        private void SetActiveNav(Button activeBtn)
        {
            BtnDashboard.Style      = (Style)FindResource("StudentNavBtn");
            BtnCourses.Style        = (Style)FindResource("StudentNavBtn");
            BtnQuiz.Style           = (Style)FindResource("StudentNavBtn");
            BtnProfile.Style        = (Style)FindResource("StudentNavBtn");
            BtnNotifications.Style  = (Style)FindResource("StudentNavBtn");

            activeBtn.Style = (Style)FindResource("StudentNavBtnActive");
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnDashboard);

            StudentContentArea.Content = new StudentDashboardView(_dbManager);

        }

        private void BtnCourses_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnCourses);
            StudentContentArea.Content = new MyClassesView(_dbManager);
        }

        private void BtnQuiz_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnQuiz);
            StudentContentArea.Content = new StudentQuizView(_dbManager);
        }

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnProfile);
            StudentContentArea.Content = new StudentProfileView();
        }

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnNotifications);
            StudentContentArea.Content = new StudentNotificationView();
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
