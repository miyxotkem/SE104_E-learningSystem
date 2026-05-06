using System.Windows;
using System.Windows.Controls;
using e_learning_app.Views;

namespace e_learning_app
{
    public partial class StudentMainWindow : Window
    {
        private readonly DatabaseManager _dbManager;

        public StudentMainWindow()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
            _dbManager.Initialize();
        }

        private void SetActiveNav(Button activeBtn)
        {
            // Reset all nav buttons to default style
            BtnDashboard.Style = (Style)FindResource("StudentNavBtn");
            BtnCourses.Style   = (Style)FindResource("StudentNavBtn");
            BtnQuiz.Style      = (Style)FindResource("StudentNavBtn");
            BtnProfile.Style   = (Style)FindResource("StudentNavBtn");
            BtnNotifications.Style = (Style)FindResource("StudentNavBtn");

            // Set active style
            activeBtn.Style = (Style)FindResource("StudentNavBtnActive");
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnDashboard);
            //StudentContentArea.Content = new StudentDashboardView();
        }

        private void BtnCourses_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnCourses);
            StudentContentArea.Content = new MyClassesView(_dbManager, "Student");
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
                        "Bạn có chắc muốn đăng xuất khỏi Student Panel?",
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
