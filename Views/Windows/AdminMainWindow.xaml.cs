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

        // --- Helper: deactivate all nav buttons --------------------------
        private void ClearNavSelection()
        {
            BtnDashboard.Style = (Style)FindResource("AdminNavBtn");
            BtnUsers.Style     = (Style)FindResource("AdminNavBtn");
            BtnSettings.Style  = (Style)FindResource("AdminNavBtn");
        }

        // --- Navigation --------------------------------------------------
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


        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ClearNavSelection();
            BtnSettings.Style = (Style)FindResource("AdminNavBtnActive");
            AdminContentArea.Content = new Views.Admin.AdminSettingsView();
        }

        private bool _isForceLogout = false;

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = CustomDialog.Confirm(
                "Bạn có chắc muốn dang xuất khỏi Admin Panel?",
                "Xác nhận dang xuất",
                "Đăng xuất", "Hủy",
                DialogType.Question);

            if (confirmed)
            {
                // Đối với Admin, chỉ cần quay về Login, không cần sign out Firebase
                var loginWin = new LoginWindow(true);
                loginWin.Show();
                _isForceLogout = true;
                this.Close();
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isForceLogout)
            {
                base.OnClosing(e);
                return;
            }

            var result = CustomDialog.ShowExit("Bạn muốn làm gì trước khi thoát Admin Panel?", "Xác nhận");
            if (result == CustomDialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == CustomDialogResult.Logout)
            {
                FirebaseService.SignOut();
                var loginWin = new LoginWindow(true);
                loginWin.Show();
            }
            base.OnClosing(e);
        }
    }
}
