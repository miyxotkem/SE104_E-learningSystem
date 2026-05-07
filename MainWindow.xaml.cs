using e_learning_app.Views;
using Google.Cloud.Firestore;
using System;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseManager _dbManager;

        public MainWindow()
        {
            InitializeComponent();

            _dbManager = new DatabaseManager();

            // XÓA PHẦN IF/ELSE CHECK ROLE Ở ĐÂY VÌ USER CHƯA LOAD XONG

            btnDashBoard.Focus();

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Query query = _dbManager.GetDb.Collection("Users").WhereEqualTo("Email", "instructortest@example.com");
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Count > 0)
                {
                    _dbManager.SetCurrentUser(snapshot.Documents[0].ConvertTo<User>());
                    this.DataContext = _dbManager.GetCurrentUser();

                    // ĐƯA LOGIC MỞ GIAO DIỆN VÀO ĐÂY (SAU KHI ĐÃ CÓ USER)
                    if (_dbManager.GetCurrentUser().Role == "Instructor")
                        MainContentArea.Content = new TeacherDashboardView(_dbManager);
                    else
                        MainContentArea.Content = new StudentDashboardView(_dbManager);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi load User: " + ex.Message);
            }
        }

        // ─── Navigation Logic (ĐÃ ĐỔI THÀNH PUBLIC ĐỂ CÁC VIEW KHÁC CÓ THỂ GỌI) ──────────

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
    }
}