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

        public string Initials =>
            string.IsNullOrWhiteSpace(FullName) ? "?" :
            string.Concat(FullName.Split(' ')
                                  .Where(w => w.Length > 0)
                                  .Take(2)
                                  .Select(w => char.ToUpper(w[0])));
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
                QuerySnapshot snap = await _db.GetDb.Collection("Users").GetSnapshotAsync();
                _allUsers = snap.Documents.Select(d =>
                {
                    var u = d.ConvertTo<User>();
                    return new AdminUserRow
                    {
                        Uid       = u.Uid,
                        FullName  = u.FullName ?? u.Email,
                        Email     = u.Email,
                        Role      = u.Role ?? "Student",
                        CreatedAt = "N/A"
                    };
                }).OrderBy(u => u.FullName).ToList();

                TxtUserCount.Text = $"{_allUsers.Count} người dùng trong hệ thống";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                TxtUserCount.Text = "Không thể tải dữ liệu";
                System.Diagnostics.Debug.WriteLine($"AdminUsersView load error: {ex.Message}");
            }
        }

        // ─── Filtering ──────────────────────────────────────────────────
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
            => SetFilter("teacher", FilterTeacher);
    }
}
