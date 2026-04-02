using e_learning_app;
using FirebaseIntegration;
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
            if(_dbManager.GetCurrentUser().Password == txtCurrentPassword.Password)
                _mainProfile.ShowNewPasswordView();
            else
                MessageBox.Show("Incorrect password");
        }
    }
}