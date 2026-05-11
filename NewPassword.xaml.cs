using e_learning_app;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class NewPassword : UserControl
    {
        private ProfileManage _mainProfile;
        private DatabaseManager _dbManager;

        public NewPassword(ProfileManage mainProfile, DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _mainProfile = mainProfile;
        }

        private async void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (txtNewPassword.Password == txtConfirmPassword.Password)
            {
                var user = _dbManager.GetCurrentUser();

                if (user != null)
                {
                    try
                    {
                        await _dbManager.UpdateFullProfile(user.Id, user);
                        CustomDialog.Show("Password updated successfully!", "Success", DialogType.Success);
                        _mainProfile.ClosePasswordView();
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show($"Failed to update: {ex.Message}", "Error", DialogType.Error);
                    }
                }
            }
            else
                CustomDialog.Show("Passwords do not match!", "Warning", DialogType.Warning);
        }
    }
}