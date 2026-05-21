using e_learning_app;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class CurrentPassword : UserControl
    {
        private ProfileManage _mainProfile;
        private DatabaseManager _dbManager;
        public CurrentPassword(ProfileManage mainProfile, DatabaseManager dbManager)
        {
            InitializeComponent();
            _mainProfile = mainProfile;
            _dbManager = dbManager;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            // Cho phép di tiếp (Vì Firestore không còn luu Password để so sánh)
            _mainProfile.ShowNewPasswordView();
        }
    }
}