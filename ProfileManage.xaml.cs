using System;
using System.Windows;
using System.Windows.Controls;
using FirebaseIntegration;
using Google.Cloud.Firestore;
using User = FirebaseIntegration.User;

namespace e_learning_app
{
    public partial class ProfileManage : UserControl
    {
        private DatabaseManager dbManager;

        public ProfileManage()
        {
            InitializeComponent();
            dbManager = new DatabaseManager();
            dbManager.Initialize();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Query query = dbManager.GetDb.Collection("Users").WhereEqualTo("Email", "john@example.com");
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                dbManager.SetCurrentUser(snapshot.Documents[0].ConvertTo<User>());

                if (snapshot.Documents.Count > 0)
                {
                    User user = await dbManager.GetUserAsync(snapshot.Documents[0].Id);
                    if (user != null)
                    {
                        txtFullName.Text = user.FullName;
                        txtEmail.Text = user.Email;
                        txtPhone.Text = user.PhoneNumber;
                        txtUsername.Text = user.Username;
                        txtRole.Text = user.Role;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading user: " + ex.Message);
            }
        }

        private void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordViewContainer.Content = new CurrentPassword(this, dbManager);
        }

        public void ShowNewPasswordView()
        {
            PasswordViewContainer.Content = new NewPassword(dbManager);
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var user = dbManager.GetCurrentUser();
                
                user.FullName = txtFullName.Text;
                user.Email = txtEmail.Text;
                user.PhoneNumber = txtPhone.Text;
                user.Username = txtUsername.Text;
                user.Role = txtRole.Text;

                await dbManager.UpdateFullProfile(user.Id, user);
                MessageBox.Show("Profile updated successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating profile: " + ex.Message);
            }
        }
    }
}