using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using e_learning_app.Class;

namespace e_learning_app.Views.Admin
{
    public partial class AdminDashboardView : UserControl
    {
        private readonly DatabaseManager _db;

        public AdminDashboardView(DatabaseManager db)
        {
            InitializeComponent();
            _db = db;
        }

        private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // Load all users from API
                var response = await ApiService.GetAsync<List<UserResponse>>("users");
                var users = response != null ? response.Select(r => 
                {
                    var u = r.Data;
                    u.Id = r.Id;
                    return u;
                }).ToList() : new List<User>();

                int totalUsers    = users.Count;
                int totalStudents = users.Count(u => u.Role?.ToLower() == "student");
                int totalTeachers = users.Count(u => u.Role?.ToLower() == "instructor");

                // Count users created in last 7 days (Bị gỡ bỏ vì không còn CreatedAt)
                int newUsers = 0;

                // Update stat cards
                TxtTotalUsers.Text    = totalUsers.ToString();
                TxtTotalStudents.Text = totalStudents.ToString();
                TxtTotalTeachers.Text = totalTeachers.ToString();
                TxtNewUsers.Text      = newUsers.ToString();

                // Show 10 users in the table
                var recent = users
                    .Take(10)
                    .ToList();

                RecentUsersGrid.ItemsSource = recent;
            }
            catch (Exception ex)
            {
                TxtTotalUsers.Text    = "Lỗi";
                TxtTotalStudents.Text = "Lỗi";
                TxtTotalTeachers.Text = "Lỗi";
                //TxtTotalCourses.Text  = "Lỗi";
                TxtNewUsers.Text      = "Lỗi";
                System.Diagnostics.Debug.WriteLine($"AdminDashboard error: {ex.Message}");
                CustomDialog.Show($"Lỗi tải dashboard: {ex.Message}", "Lỗi tải dữ liệu", DialogType.Error);
            }
        }
    }
}
