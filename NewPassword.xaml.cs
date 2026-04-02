using e_learning_app;
using FirebaseIntegration;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app
{
    public partial class NewPassword : UserControl
    {
        private DatabaseManager _dbManager;

        public NewPassword()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
        }

        public NewPassword(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (txtNewPassword.Password == txtConfirmPassword.Password)
            {
                var user = _dbManager.GetCurrentUser();

                if (txtNewPassword.Password == user.Password)
                    MessageBox.Show("Password is the same!");
                else if (user != null)
                {
                    user.Password = txtNewPassword.Password;
                    try
                    {
                        await _dbManager.UpdateFullProfile(user.Id, user);
                        MessageBox.Show("Password updated successfully!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to update: {ex.Message}");
                    }
                }
            }
            else
                MessageBox.Show("Passwords not match!");
        }
    }
}