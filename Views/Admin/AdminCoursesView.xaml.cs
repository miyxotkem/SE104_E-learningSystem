using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using e_learning_app.Class;

namespace e_learning_app.Views.Admin
{
    public class AdminCourseRow
    {
        public string CourseId     { get; set; }
        public string CourseName   { get; set; }
        public string TeacherName  { get; set; }
        public int    StudentCount { get; set; }
        public string CreatedAt    { get; set; }
    }

    public partial class AdminCoursesView : UserControl
    {
        private readonly DatabaseManager _db;

        public AdminCoursesView(DatabaseManager db)
        {
            InitializeComponent();
            _db = db;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var allCourses = await ApiService.GetAsync<List<CourseResponse>>("courses");

                if (allCourses != null)
                {
                    var rows = allCourses.Select(c =>
                    {
                        var dict = c.Data;
                        return new AdminCourseRow
                        {
                            CourseId = c.Id,
                            CourseName = !string.IsNullOrEmpty(dict?.ClassName) ? dict.ClassName : "—",
                            TeacherName = !string.IsNullOrEmpty(dict?.InstructorId) ? dict.InstructorId : "—",
                            StudentCount = dict?.StudentCount ?? 0,
                            CreatedAt = dict != null ? dict.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy") : "—"
                        };
                    }).OrderBy(c => c.CourseName).ToList();

                    TxtCourseCount.Text = $"{rows.Count} khóa học trong hệ thống";
                    CoursesGrid.ItemsSource = rows;
                }
                else
                {
                    TxtCourseCount.Text = "0 khóa học trong hệ thống";
                    CoursesGrid.ItemsSource = new List<AdminCourseRow>();
                }
            }
            catch (Exception ex)
            {
                TxtCourseCount.Text = "Không thể tải dữ liệu";
                System.Diagnostics.Debug.WriteLine($"AdminCoursesView error: {ex.Message}");
            }
        }
    }
}
