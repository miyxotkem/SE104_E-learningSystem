using Google.Cloud.Firestore;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Win32;

namespace e_learning_app
{
    public partial class ProfileManage : UserControl
    {
        private DatabaseManager _dbManager;
        private string _pendingAvatarUrl = null;

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
                        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                        {
                            try
                            {
                                imgAvatar.ImageSource = new BitmapImage(new Uri(user.ProfileImageUrl));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tải thông tin: " + ex.Message, "Lỗi", DialogType.Error);
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
                CustomDialog.Show("Hệ thống đã gửi link khôi phục vào Email của bạn. Hãy kiểm tra nhé!", "Thành công", DialogType.Success);
            }
            else
            {
                CustomDialog.Show("Email không chính xác hoặc không tồn tại. Hãy kiểm tra nhé!", "Lỗi", DialogType.Error);
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

                if (_pendingAvatarUrl != null)
                {
                    user.ProfileImageUrl = _pendingAvatarUrl;
                    if (string.IsNullOrEmpty(_pendingAvatarUrl))
                    {
                        await e_learning_app.Class.ApiService.DeleteAsync("users/profile/avatar");
                    }
                    else
                    {
                        await e_learning_app.Class.ApiService.PutAsync("users/profile/avatar", new { ProfileImageUrl = _pendingAvatarUrl });
                    }
                    _pendingAvatarUrl = null;
                }

                await _dbManager.UpdateFullProfile(user.Id, user);
                
                // Cập nhật lại local state để các màn hình khác (Sidebar) thấy được sự thay đổi
                _dbManager.SetCurrentUser(user);

                CustomDialog.Show("Cập nhật thông tin thành công!", "Thông báo", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi cập nhật: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private void txtFullName_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private async void btnChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Account account = new Account("drg8swbxp", "167723827683986", "3aclNKhg3htYds76wcUrxjTdnRU");
                    Cloudinary cloudinary = new Cloudinary(account);
                    cloudinary.Api.Secure = true;

                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(openFileDialog.FileName),
                        UseFilename = true,
                        UniqueFilename = true
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);
                    
                    string newAvatarUrl = uploadResult.SecureUrl.ToString();
                    
                    _pendingAvatarUrl = newAvatarUrl;
                    
                    imgAvatar.ImageSource = new BitmapImage(new Uri(newAvatarUrl));
                    CustomDialog.Show("Ảnh đại diện đã được thay đổi. Nhấn Lưu thay đổi để áp dụng!", "Thông báo", DialogType.Success);
                }
                catch (Exception ex)
                {
                    CustomDialog.Show("Lỗi cập nhật ảnh đại diện: " + ex.Message, "Lỗi", DialogType.Error);
                }
            }
        }

        private void btnDeleteAvatar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pendingAvatarUrl = "";
                imgAvatar.ImageSource = null;
                CustomDialog.Show("Ảnh đại diện đã được xóa. Nhấn Lưu thay đổi để áp dụng!", "Thông báo", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi xóa ảnh đại diện: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }
    }
}