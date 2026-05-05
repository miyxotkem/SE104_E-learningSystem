using System.Windows;
using System.Windows.Controls;
using e_learning_app.Views;

namespace e_learning_app
{
    public partial class StudentMainWindow : Window
    {
        public StudentMainWindow()
        {
            InitializeComponent();
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
            StudentContentArea.Content = new StudentCourseView();
        }

        private void BtnQuiz_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav(BtnQuiz);
            StudentContentArea.Content = new StudentQuizView();
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
    }
}
