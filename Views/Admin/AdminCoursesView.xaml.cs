using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
                QuerySnapshot snap = await _db.GetDb.Collection("Courses").GetSnapshotAsync();

                var rows = snap.Documents.Select(d =>
                {
                    var dict = d.ToDictionary();
                    return new AdminCourseRow
                    {
                        CourseId    = d.Id,
                        CourseName  = dict.ContainsKey("CourseName")  ? dict["CourseName"]?.ToString()  : "—",
                        TeacherName = dict.ContainsKey("TeacherName") ? dict["TeacherName"]?.ToString() : "—",
                        StudentCount= dict.ContainsKey("StudentCount") ? Convert.ToInt32(dict["StudentCount"]) : 0,
                        CreatedAt   = dict.ContainsKey("CreatedAt")   ? dict["CreatedAt"]?.ToString()   : "—"
                    };
                }).OrderBy(c => c.CourseName).ToList();

                TxtCourseCount.Text = $"{rows.Count} khóa học trong hệ thống";
                CoursesGrid.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                TxtCourseCount.Text = "Không thể tải dữ liệu";
                System.Diagnostics.Debug.WriteLine($"AdminCoursesView error: {ex.Message}");
            }
        }
    }
}
