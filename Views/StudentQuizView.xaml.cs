using e_learning_app;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Input;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class StudentQuizView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private List<Exam> _allExams = new();
        private List<ExamSubmission> _studentSubmissions = new();

        public StudentQuizView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            RenderExams();
        }

        private async Task LoadDataAsync()
        {
            if (_dbManager == null || _dbManager.GetDb == null) return;
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            try
            {
                // 1. Get enrolled courses
                var registrationsSnap = await _dbManager.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("userId", currentUser.Id)
                    .WhereEqualTo("status", "accepted")
                    .GetSnapshotAsync();

                var enrolledCourseIds = registrationsSnap.Documents.Select(d => d.GetValue<string>("courseId")).ToList();
                if (enrolledCourseIds.Count == 0)
                {
                    _allExams.Clear();
                    return;
                }

                // 2. Get exams for these courses (chunking if > 10)
                _allExams.Clear();
                for (int i = 0; i < enrolledCourseIds.Count; i += 10)
                {
                    var chunk = enrolledCourseIds.Skip(i).Take(10).ToList();
                    var snapshot = await _dbManager.GetDb.Collection("exams")
                        .WhereEqualTo("IsPublished", true)
                        .WhereIn("ClassId", chunk)
                        .GetSnapshotAsync();

                    _allExams.AddRange(snapshot.Documents.Select(doc => doc.ConvertTo<Exam>()));
                }

                // 3. Get student's submissions to show status
                var submissionsSnap = await _dbManager.GetDb.Collection("exam_submissions")
                    .WhereEqualTo("StudentId", currentUser.Id)
                    .GetSnapshotAsync();
                
                _studentSubmissions = submissionsSnap.Documents.Select(doc => doc.ConvertTo<ExamSubmission>()).ToList();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi tải bài kiểm tra: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        private void RenderExams()
        {
            var examList = new List<object>();
            foreach (var exam in _allExams)
            {
                var submission = _studentSubmissions.FirstOrDefault(s => s.ExamId == exam.Id);
                bool isDone = submission != null;

                // Status configuration
                string statusText = isDone ? "Hoàn thành" : (exam.IsActive ? "Đang mở" : "Đã đóng");
                string statusColor = isDone ? "#3B82F6" : (exam.IsActive ? "#16A34A" : "#64748B");
                string statusBg = isDone ? "#EFF6FF" : (exam.IsActive ? "#DCFCE7" : "#F1F5F9");

                // Highest Score
                var examSubmissions = _studentSubmissions.Where(s => s.ExamId == exam.Id).ToList();
                double? maxScore = examSubmissions.Any() ? examSubmissions.Max(s => s.Score) : null;

                examList.Add(new
                {
                    Exam = exam,
                    StatusText = statusText,
                    StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusColor)),
                    StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(statusBg)),
                    MaxScoreDisplay = maxScore.HasValue ? $"{maxScore.Value:F1}" : "--"
                });
            }

            ExamsList.ItemsSource = examList;
        }

        private void ExamsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExamsList.SelectedItem != null)
            {
                dynamic selectedItem = ExamsList.SelectedItem;
                NavigateToHistory(selectedItem.Exam);

                ExamsList.SelectedItem = null;
            }
        }

        private void NavigateToHistory(Exam exam)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new QuizHistoryView(_dbManager, exam);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new QuizHistoryView(_dbManager, exam);
        }

        private void BtnTake_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string examId)
            {
                var selectedExam = _allExams.FirstOrDefault(x => x.Id == examId);
            }
        }
    }
}
