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
            _dbManager.Initialize();

            MainContentArea.Content = new TeacherDashboardView(_dbManager);
            btnDashBoard.Focus();

            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Query query = _dbManager.GetDb.Collection("Users").WhereEqualTo("Email", "john@example.com");
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            _dbManager.SetCurrentUser(snapshot.Documents[0].ConvertTo<User>());

            this.DataContext = _dbManager.GetCurrentUser();
        }

        // ─── Navigation Logic ──────────────────────────────────────────

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new TeacherDashboardView(_dbManager);
        }

        private void NavClasses_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new MyClassesView(_dbManager);
        }

        private void NavQuestions_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new QuestionBankView();
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ReportsView(_dbManager);
        }

        private void NavNotifications_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new NotificationsView();
        }

        private void NavSemestersettings_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new SemesterSettingsView();
        }

        public void NavigateTo(UserControl view) => MainContentArea.Content = view;

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = new ProfileManage(_dbManager);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                        "Bạn có chắc muốn đăng xuất khỏi Teacher Panel?",
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