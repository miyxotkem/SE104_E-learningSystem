using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class QuizResultDetailView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Exam _exam;
        private readonly ExamSubmission _submission;
        private List<ExamQuestion> _allQuestions = new();
        private List<object> _processedQuestions = new();

        public QuizResultDetailView(DatabaseManager dbManager, Exam exam, ExamSubmission submission)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
            _submission = submission;
            
            LoadDetails();
        }

        private async void LoadDetails()
        {
            TxtSubTitle.Text = $"Nộp lúc: {_submission.SubmittedAt:dd/MM/yyyy HH:mm}";
            TxtPercentage.Text = $"Tỷ lệ điểm: {(_submission.Percentage):F1}%";
            
            bool isPassed = _submission.Percentage >= _exam.PassingScore;
            TxtResultStatus.Text = $"Trạng thái: {(isPassed ? "ĐẠT" : "KHÔNG ĐẠT")}";

            try
            {
                _allQuestions = await _dbManager.GetExamQuestionsAsync(_exam.Id);
                
                double maxScore = _allQuestions.Sum(q => q.Points);
                TxtScoreLarge.Text = $"{_submission.Score:F1} / {maxScore:F1}";

                ProcessQuestions();
                ApplyFilter("All");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải chi tiết: {ex.Message}");
            }
        }

        private void ProcessQuestions()
        {
            _processedQuestions.Clear();
            int index = 1;

            foreach (var q in _allQuestions)
            {
                var studentAnsResponse = _submission.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                bool isCorrect = studentAnsResponse?.IsCorrect ?? false;
                
                int studentChoiceIdx = -1;
                if (studentAnsResponse != null && int.TryParse(studentAnsResponse.StudentAnswer, out int val))
                    studentChoiceIdx = val;

                var options = new List<object>();
                if (q.Options != null)
                {
                    for (int i = 0; i < q.Options.Count; i++)
                    {
                        bool isStudentChoice = (i == studentChoiceIdx);
                        bool isCorrectChoice = (i == q.CorrectAnswerIndex);

                        Brush bg = Brushes.Transparent;
                        Brush border = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                        Brush text = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                        string icon = "";

                        if (isStudentChoice && isCorrectChoice)
                        {
                            bg = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)); // Green bg
                            border = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                            text = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                            icon = "✓";
                        }
                        else if (isStudentChoice && !isCorrectChoice)
                        {
                            bg = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)); // Red bg
                            border = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                            text = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B));
                            icon = "✕";
                        }
                        else if (!isStudentChoice && isCorrectChoice)
                        {
                            border = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                            text = new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D));
                            icon = "✓";
                        }

                        options.Add(new
                        {
                            Prefix = $"{(char)('A' + i)}.",
                            Content = q.Options[i],
                            Background = bg,
                            BorderBrush = border,
                            TextBrush = text,
                            Icon = icon
                        });
                    }
                }

                _processedQuestions.Add(new
                {
                    IsCorrect = isCorrect,
                    QuestionNumber = $"Câu {index}",
                    QuestionContent = q.Content,
                    ResultText = isCorrect ? "ĐÚNG" : "SAI",
                    ResultBg = isCorrect ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)) : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)),
                    ResultFg = isCorrect ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    PointsDisplay = $"{(isCorrect ? q.Points : 0)} / {q.Points} điểm",
                    Options = options
                });
                index++;
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string filter)
            {
                ApplyFilter(filter);
            }
        }

        private void ApplyFilter(string filter)
        {
            IEnumerable<object> filtered = _processedQuestions;

            if (filter == "Correct")
                filtered = _processedQuestions.Where(q => (bool)q.GetType().GetProperty("IsCorrect").GetValue(q) == true);
            else if (filter == "Incorrect")
                filtered = _processedQuestions.Where(q => (bool)q.GetType().GetProperty("IsCorrect").GetValue(q) == false);

            ItemsQuestions.ItemsSource = filtered.ToList();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new QuizHistoryView(_dbManager, _exam);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new QuizHistoryView(_dbManager, _exam);
        }
    }
}
