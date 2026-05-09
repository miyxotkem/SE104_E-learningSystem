using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class QuizHistoryView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Exam _exam;
        private List<ExamSubmission> _submissions = new();

        public QuizHistoryView(DatabaseManager dbManager, Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
            
            LoadData();
        }

        private async void LoadData()
        {
            TxtExamTitle.Text = _exam.Title;
            TxtExamDescription.Text = _exam.Description;
            TxtTimeLimit.Text = $"{_exam.TimeLimitMinutes} phút";
            TxtPassingScore.Text = $"{(int)_exam.PassingScore}%";
            TxtMaxAttempts.Text = $"{_exam.MaxAttempts} lần";
            TxtTotalQuestions.Text = $"{_exam.TotalQuestions} câu";

            if (_dbManager == null) return;
            var user = _dbManager.GetCurrentUser();
            if (user == null) return;

            try
            {
                // Fetch all submissions for this student and exam
                var snap = await _dbManager.GetDb.Collection("exam_submissions")
                    .WhereEqualTo("ExamId", _exam.Id)
                    .WhereEqualTo("StudentId", user.Id)
                    .GetSnapshotAsync();

                _submissions = snap.Documents
                    .Select(doc => doc.ConvertTo<ExamSubmission>())
                    .OrderByDescending(s => s.SubmittedAt)
                    .ToList();

                // Fetch questions to get total possible points
                var questions = await _dbManager.GetExamQuestionsAsync(_exam.Id);
                double totalPoints = questions.Sum(q => q.Points);
                if (totalPoints == 0) totalPoints = _exam.TotalQuestions > 0 ? _exam.TotalQuestions : 10;

                // Highest Score Calculation
                double maxScoreValue = _submissions.Any() ? _submissions.Max(s => s.Score) : 0;
                TxtHighestScore.Text = $"{maxScoreValue:F1} / {totalPoints:F1}";

                // Check attempts limit
                if (_submissions.Count >= _exam.MaxAttempts && !_exam.AllowMultipleAttempts)
                {
                    BtnStartQuiz.IsEnabled = false;
                    BtnStartQuiz.Content = "❌ Hết lượt làm bài";
                }

                // Map to ViewModel-like structure for display
                var displayList = _submissions.Select(s => new
                {
                    Submission = s, // Keep reference for click
                    s.SubmittedAt,
                    ScoreDisplay = $"{s.Score:F1} / {totalPoints:F1}",
                    TimeDisplay = $"Thời gian làm bài: {TimeSpan.FromSeconds(s.TimeSpentSeconds):mm\\:ss}",
                    StatusText = s.Percentage >= _exam.PassingScore ? "Đạt" : "Không đạt",
                    StatusBrush = s.Percentage >= _exam.PassingScore ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                }).ToList();

                ItemsHistory.ItemsSource = displayList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải lịch sử: {ex.Message}");
            }
        }

        private void AttemptCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                // Accessing anonymous type property via dynamic or reflection is tricky in C#, 
                // but since we know the structure of displayList:
                var context = fe.DataContext;
                var submission = (ExamSubmission)context.GetType().GetProperty("Submission")?.GetValue(context);

                if (submission != null)
                {
                    if (Window.GetWindow(this) is MainWindow mw)
                        mw.MainContentArea.Content = new QuizResultDetailView(_dbManager, _exam, submission);
                    else if (Window.GetWindow(this) is StudentMainWindow smw)
                        smw.StudentContentArea.Content = new QuizResultDetailView(_dbManager, _exam, submission);
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new StudentQuizView(_dbManager);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new StudentQuizView(_dbManager);
        }

        private void BtnStartQuiz_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new TakeQuizView(_dbManager, _exam);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new TakeQuizView(_dbManager, _exam);
        }
    }
}
