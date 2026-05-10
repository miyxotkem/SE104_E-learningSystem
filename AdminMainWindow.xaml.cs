using Google.Apis.Util.Store;
using System.Windows;

namespace e_learning_app
{
    public partial class AdminMainWindow : Window
    {
        private readonly DatabaseManager _dbManager;

        public AdminMainWindow()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
            _dbManager.Initialize();

            // Default view: Admin Dashboard
            AdminContentArea.Content = new Views.Admin.AdminDashboardView(_dbManager);
            BtnDashboard.Focus();
        }

        // ─── Helper: deactivate all nav buttons ──────────────────────────
        private void ClearNavSelection()
        {
            BtnDashboard.Style = (Style)FindResource("AdminNavBtn");
            BtnUsers.Style     = (Style)FindResource("AdminNavBtn");
            BtnReports.Style   = (Style)FindResource("AdminNavBtn");
            BtnSettings.Style  = (Style)FindResource("AdminNavBtn");
        }

        // ─── Navigation ──────────────────────────────────────────────────
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            BtnDashboard.Style = (Style)FindResource("AdminNavBtnActive");
            AdminContentArea.Content = new Views.Admin.AdminDashboardView(_dbManager);
        }

        private void BtnUsers_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            BtnUsers.Style = (Style)FindResource("AdminNavBtnActive");
            AdminContentArea.Content = new Views.Admin.AdminUsersView(_dbManager);
        }


        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            BtnReports.Style = (Style)FindResource("AdminNavBtnActive");
            AdminContentArea.Content = new Views.Admin.AdminReportsView(_dbManager);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            BtnSettings.Style = (Style)FindResource("AdminNavBtnActive");
            AdminContentArea.Content = new Views.Admin.AdminSettingsView();
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            string credPath = "gg.auth.api";
            var dataStore = new FileDataStore(credPath, true);
            await dataStore.ClearAsync();
            var result = MessageBox.Show(
                "Bạn có chắc muốn đăng xuất khỏi Admin Panel?",
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
