using Google.Cloud.Firestore;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app
{
    public partial class ProfileManage : UserControl
    {
        private DatabaseManager _dbManager;

        public ProfileManage(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dbManager.GetCurrentUser() != null)
                {
                    User user = await _dbManager.GetUserAsync(_dbManager.GetCurrentUser().Id);
                    if (user != null)
                    {
                        txtFullName.Text = user.FullName;
                        txtEmail.Text = user.Email;
                        txtPhone.Text = user.PhoneNumber;
                        txtRole.Text = user.Role;
                        txtCreatedAt.Text = user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải thông tin: " + ex.Message);
            }
        }

        private async void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text;
            if (string.IsNullOrWhiteSpace(email)) return;

            btnChangePassword.IsEnabled = false;
            bool issent = await FirebaseService.SendPasswordResetAsync(email);

            if (issent)
            {
                MessageBox.Show("Hệ thống đã gửi link khôi phục vào Email của bạn. Hãy kiểm tra nhé!");
            }
            else
            {
                MessageBox.Show("Email không chính xác hoặc không tồn tại. Hãy kiểm tra nhé!");
            }
            btnChangePassword.IsEnabled = true;
        }

        public void ShowNewPasswordView()
        {
            FullScreenOverlay.Content = new NewPassword(this, _dbManager);
        }

        public void ClosePasswordView()
        {
            FullScreenOverlay.Visibility = Visibility.Collapsed;
            MainProfileUI.Visibility = Visibility.Visible;
            FullScreenOverlay.Content = null;
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnSave.IsEnabled = false;
                var user = _dbManager.GetCurrentUser();
                if (user == null) return;

                user.FullName = txtFullName.Text;
                user.Email = txtEmail.Text;
                user.PhoneNumber = txtPhone.Text;

                await _dbManager.UpdateFullProfile(user.Id, user);
                
                // Cập nhật lại local state để các màn hình khác (Sidebar) thấy được sự thay đổi
                _dbManager.SetCurrentUser(user);

                MessageBox.Show("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi cập nhật: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private void txtFullName_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}