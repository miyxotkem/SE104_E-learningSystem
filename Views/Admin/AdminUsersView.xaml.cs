using e_learning_app;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app.Views.Admin
{
    // DTO for the Users grid — adds computed Initials property
    public class AdminUserRow
    {
        public string Uid        { get; set; }
        public string FullName   { get; set; }
        public string Email      { get; set; }
        public string Role       { get; set; }
        public string CreatedAt  { get; set; }
        public bool IsBlocked    { get; set; }

        public string Initials =>
            string.IsNullOrWhiteSpace(FullName) ? "?" :
            string.Concat(FullName.Split(' ')
                                  .Where(w => w.Length > 0)
                                  .Take(2)
                                  .Select(w => char.ToUpper(w[0])));

        // UI Helpers
        public string BlockText => IsBlocked ? "🔓 Mở khóa" : "🔒 Khóa";
        public System.Windows.Media.Brush BlockTextBrush => IsBlocked ? System.Windows.Media.Brushes.Green : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
        public System.Windows.Media.Brush IsBlockedBrush => IsBlocked ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCFCE7")) : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
    }

    public partial class AdminUsersView : UserControl
    {
        private readonly DatabaseManager _db;
        private List<AdminUserRow> _allUsers = new();
        private string _currentRoleFilter = "all";

        public AdminUsersView(DatabaseManager db)
        {
            InitializeComponent();
            _db = db;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Xóa list cu trước khi load lại
                _allUsers.Clear();
                var usersResponse = await e_learning_app.Class.ApiService.GetAsync<List<e_learning_app.Class.UserResponse>>("users");
                if (usersResponse != null)
                {
                    _allUsers = usersResponse.Select(r =>
                    {
                        var u = r.Data;
                        return new AdminUserRow
                        {
                            Uid       = r.Id,
                            FullName  = u.FullName ?? u.Email,
                            Email     = u.Email,
                            Role      = u.Role ?? "Student",
                            IsBlocked = u.IsBlocked,
                            CreatedAt = u.CreatedAt == default ? "N/A" : u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy")
                        };
                    }).OrderBy(u => u.FullName).ToList();
                }

                TxtUserCount.Text = $"{_allUsers.Count} người dùng trong hệ thống";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                TxtUserCount.Text = "Không thể tải dữ liệu";
                System.Diagnostics.Debug.WriteLine($"AdminUsersView load error: {ex.Message}");
            }
        }

        // --- Filtering --------------------------------------------------
        private void ApplyFilter()
        {
            var search = TxtSearch.Text?.ToLower() ?? "";
            var filtered = _allUsers.Where(u =>
            {
                bool matchRole = _currentRoleFilter == "all" ||
                                 u.Role?.ToLower() == _currentRoleFilter;
                bool matchSearch = string.IsNullOrWhiteSpace(search) ||
                                   (u.FullName?.ToLower().Contains(search) == true) ||
                                   (u.Email?.ToLower().Contains(search) == true);
                return matchRole && matchSearch;
            }).ToList();

            UsersGrid.ItemsSource = filtered;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void SetFilter(string role, Border activeBtn)
        {
            _currentRoleFilter = role;

            // Reset all pills
            FilterAll.Background     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            FilterStudent.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            FilterTeacher.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));

            // Activate selected
            activeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDE9FE"));
            ApplyFilter();
        }

        private void FilterAll_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SetFilter("all", FilterAll);

        private void FilterStudent_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SetFilter("student", FilterStudent);

        private void FilterTeacher_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SetFilter("instructor", FilterTeacher);

        private async void BtnChangeRole_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AdminUserRow row)
            {
                if (string.IsNullOrEmpty(row.Uid))
                {
                    CustomDialog.Show("Không tìm thấy ID người dùng!", "Lỗi", DialogType.Error);
                    return;
                }
                string newRole = row.Role == "Instructor" ? "Student" : "Instructor";
                
                var confirmed = CustomDialog.Confirm($"Bạn có chắc muốn đổi vai trò của {row.FullName} thành {newRole} không?", 
                                           "Xác nhận thay đổi", "Đồng ý", "Hủy", DialogType.Question);
                
                if (confirmed)
                {
                    try
                    {
                        await e_learning_app.Class.ApiService.PutAsync($"users/{row.Uid}/role", new { Role = newRole });
                        CustomDialog.Show("Cập nhật vai trò thành công!", "Thông báo", DialogType.Success);
                        
                        // Load lại dữ liệu
                        UserControl_Loaded(null, null);
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show($"Lỗi: {ex.Message}", "Lỗi", DialogType.Error);
                    }
                }
            }
        }

        private async void BtnBlockUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AdminUserRow row)
            {
                bool newBlockStatus = !row.IsBlocked;
                string action = newBlockStatus ? "khóa" : "mở khóa";

                var confirmed = CustomDialog.Confirm($"Bạn có chắc muốn {action} tài khoản {row.FullName}?", "Xác nhận", "Thực hiện", "Hủy", DialogType.Warning);
                if (confirmed)
                {
                    try
                    {
                        await e_learning_app.Class.ApiService.PutAsync($"users/{row.Uid}/block", new { IsBlocked = newBlockStatus });
                        CustomDialog.Show($"Đã {action} tài khoản thành công!", "Thông báo", DialogType.Success);
                        UserControl_Loaded(null, null);
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show($"Lỗi: {ex.Message}", "Lỗi", DialogType.Error);
                    }
                }
            }
        }

        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AdminUserRow row)
            {
                var confirmed = CustomDialog.Confirm($"CẢNH BÁO: Bạn có chắc muốn XÓA VINH VIỄN tài khoản {row.FullName}? \n\nHành động này không thể hoàn tác!", "Xác nhận xóa", "Xóa vinh viễn", "Hủy", DialogType.Error);
                if (confirmed)
                {
                    try
                    {
                        await e_learning_app.Class.ApiService.DeleteAsync($"users/{row.Uid}");
                        CustomDialog.Show("Đã xóa tài khoản thành công!", "Thông báo", DialogType.Success);
                        UserControl_Loaded(null, null);
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show($"Lỗi: {ex.Message}", "Lỗi", DialogType.Error);
                    }
                }
            }
        }
    }
}
