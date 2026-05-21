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
        private System.Windows.Threading.DispatcherTimer _pollingTimer;

        public StudentQuizView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;

            this.Unloaded += (s, e) =>
            {
                _pollingTimer?.Stop();
            };
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _pollingTimer = new System.Windows.Threading.DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(15);
            _pollingTimer.Tick += async (s, args) => await FetchDataAsync();
            _pollingTimer.Start();

            FetchDataAsync();
        }

        private async Task FetchDataAsync()
        {
            try
            {
                var examsResponse = await ApiService.GetAsync<List<ExamResponse>>("exams/my-exams");
                if (examsResponse != null)
                {
                    _allExams = examsResponse.Select(r => {
                        var ex = r.Data;
                        ex.Id = r.Id;
                        return ex;
                    }).ToList();
                }

                var subsResponse = await ApiService.GetAsync<List<ExamSubmissionResponse>>("exams/my-history");
                if (subsResponse != null)
                {
                    _studentSubmissions = subsResponse.Select(r => r.Data).ToList();
                }

                RenderExams();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Không thể tải danh sách bài thi.\nLý do: {ex.Message}", "Lỗi tải dữ liệu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Console.WriteLine("Lỗi fetch exams: " + ex.Message);
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
