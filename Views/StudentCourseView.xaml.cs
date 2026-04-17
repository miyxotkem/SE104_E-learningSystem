using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e_learning_app.Views
{
    public partial class StudentCourseView : UserControl
    {
        private DatabaseManager _dbManager;
        private Course _course;

        public StudentCourseView()
        {
            InitializeComponent();
        }

        public StudentCourseView(DatabaseManager dbManager, Course course)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _course = course;

            if (_course != null)
            {
                TxtCourseTitle.Text = _course.Title;
                // Currently just putting a placeholder for current specific video.
                TxtCourseSubtitle.Text = $"Khóa học: {_course.ClassName}  ·  Mã: {_course.Id}";
            }
        }

        private void BtnBack_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var studentWin = Window.GetWindow(this) as StudentMainWindow;
            if (studentWin != null)
            {
                studentWin.StudentContentArea.Content = new MyClassesView(_dbManager, "Student");
            }
        }
    }
}
