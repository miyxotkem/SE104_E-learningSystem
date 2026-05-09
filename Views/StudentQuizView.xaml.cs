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
                MessageBox.Show($"Lỗi tải bài kiểm tra: {ex.Message}");
            }
        }

        private void RenderExams()
        {
            ExamsPanel.Children.Clear();
            foreach (var exam in _allExams)
            {
                var submission = _studentSubmissions.FirstOrDefault(s => s.ExamId == exam.Id);
                bool isDone = submission != null;

                var card = new Border
                {
                    Width = 300,
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 16, 16),
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(12),
                    Effect = new DropShadowEffect { BlurRadius = 10, Opacity = 0.05, ShadowDepth = 2 }
                };

                var sp = new StackPanel();

                // Status tag
                string statusText = isDone ? "Hoàn thành" : (exam.IsActive ? "Đang mở" : "Đã đóng");
                Color statusColor = isDone ? Color.FromRgb(0x3B, 0x82, 0xF6) : (exam.IsActive ? Color.FromRgb(0x16, 0xA3, 0x4A) : Color.FromRgb(0x64, 0x74, 0x8B));
                Color statusBg = isDone ? Color.FromRgb(0xEF, 0xF6, 0xFF) : (exam.IsActive ? Color.FromRgb(0xDC, 0xFC, 0xE7) : Color.FromRgb(0xF1, 0xF5, 0xF9));

                var tag = new Border
                {
                    Background = new SolidColorBrush(statusBg),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = new TextBlock
                    {
                        Text = statusText,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(statusColor)
                    }
                };
                sp.Children.Add(tag);

                // Title
                sp.Children.Add(new TextBlock
                {
                    Text = exam.Title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
                });

                // Description
                sp.Children.Add(new TextBlock
                {
                    Text = exam.Description,
                    FontSize = 13,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                // Meta
                sp.Children.Add(new TextBlock
                {
                    Text = $"⏱️ {exam.TimeLimitMinutes} phút   •   🎯 Giới hạn: {(int)exam.PassingScore}%",
                    FontSize = 12,
                    Foreground = Brushes.DimGray,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Highest Score
                var examSubmissions = _studentSubmissions.Where(s => s.ExamId == exam.Id).ToList();
                double? maxScore = examSubmissions.Any() ? examSubmissions.Max(s => s.Score) : null;

                var scoreStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                scoreStack.Children.Add(new TextBlock { Text = "🏆 Điểm cao nhất: ", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
                scoreStack.Children.Add(new TextBlock 
                { 
                    Text = maxScore.HasValue ? $"{maxScore.Value:F1}" : "--", 
                    FontSize = 12, 
                    FontWeight = FontWeights.Bold, 
                    Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)) 
                });
                sp.Children.Add(scoreStack);

                card.Child = sp;
                
                // Navigate to History View on card click
                card.Cursor = Cursors.Hand;
                card.MouseLeftButtonDown += (s, e) => {
                    NavigateToHistory(exam);
                };

                ExamsPanel.Children.Add(card);
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
                if (selectedExam != null) NavigateToHistory(selectedExam);
            }
        }
    }
}
