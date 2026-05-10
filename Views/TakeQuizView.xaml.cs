using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using e_learning_app;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class TakeQuizView : UserControl
    {
        private Exam _exam;
        private DatabaseManager _dbManager;
        private List<ExamQuestion> _questions = new();
        private int _currentIndex = 0;
        private Dictionary<string, string> _answers = new(); // QuestionId -> SelectedIndex (string)

        public TakeQuizView(DatabaseManager dbManager, Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;
        }

        private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_exam != null)
            {
                TxtQuizTitle.Text = $"📝  {_exam.Title}";
                TxtQuizMeta.Text = $"Ngày diễn ra: {_exam.ScheduledDate:dd/MM/yyyy HH:mm}  ·  {_exam.TimeLimitMinutes} phút";
                
                await LoadQuestionsAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadQuestionsAsync()
        {
            try
            {
                _questions = await _dbManager.GetExamQuestionsAsync(_exam.Id);
                _questions = _questions.OrderBy(q => q.QuestionOrder).ToList();

                if (_questions.Count > 0)
                {
                    RenderQuestionMap();
                    DisplayQuestion(0);
                }
                else
                {
                    TxtQuestionText.Text = "Không có câu hỏi nào trong bài thi này.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải câu hỏi: {ex.Message}");
            }
        }

        private void DisplayQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count) return;
            _currentIndex = index;

            var q = _questions[index];
            TxtQuestionNumber.Text = $"Câu {index + 1}";
            TxtQuestionText.Text = q.Content;
            
            // Clear and render answers
            AnswersContainer.Children.Clear();
            if (q.Type == QuestionType.MultipleChoice)
            {
                for (int i = 0; i < q.Options.Count; i++)
                {
                    var rb = new RadioButton
                    {
                        Style = (Style)FindResource("AnswerRadioBtn"),
                        GroupName = $"Q_{q.Id}",
                        Tag = i.ToString(),
                        Content = new TextBlock 
                        { 
                            FontSize = 15, 
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B)),
                            Inlines = { 
                                new System.Windows.Documents.Run { Text = $"{(char)('A' + i)}. ", FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBlue") },
                                new System.Windows.Documents.Run { Text = q.Options[i] }
                            }
                        }
                    };

                    if (_answers.ContainsKey(q.Id) && _answers[q.Id] == i.ToString())
                    {
                        rb.IsChecked = true;
                    }

                    rb.Checked += (s, e) => {
                        _answers[q.Id] = (s as RadioButton).Tag.ToString();
                        UpdateQuestionMap();
                    };

                    AnswersContainer.Children.Add(rb);
                }
            }

            // Update Map selection
            UpdateQuestionMap();

            // Update buttons visibility
            BtnNext.Content = (index == _questions.Count - 1) ? "Xem lại bài" : "Câu tiếp theo →";
        }

        private void RenderQuestionMap()
        {
            QuestionMapPanel.Children.Clear();
            for (int i = 0; i < _questions.Count; i++)
            {
                var btn = new Button
                {
                    Style = (Style)FindResource("QMapBtn"),
                    Content = (i + 1).ToString(),
                    Tag = i,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9)),
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8))
                };
                btn.Click += (s, e) => DisplayQuestion((int)(s as Button).Tag);
                QuestionMapPanel.Children.Add(btn);
            }
        }

        private void UpdateQuestionMap()
        {
            for (int i = 0; i < QuestionMapPanel.Children.Count; i++)
            {
                if (QuestionMapPanel.Children[i] is Button btn)
                {
                    int idx = (int)btn.Tag;
                    bool isAnswered = _answers.ContainsKey(_questions[idx].Id);
                    bool isCurrent = idx == _currentIndex;

                    if (isCurrent)
                    {
                        btn.Background = (System.Windows.Media.Brush)FindResource("PrimaryBlue");
                        btn.Foreground = System.Windows.Media.Brushes.White;
                    }
                    else if (isAnswered)
                    {
                        btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0xFC, 0xE7));
                        btn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A));
                    }
                    else
                    {
                        btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
                        btn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
                    }
                }
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                DisplayQuestion(_currentIndex - 1);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _questions.Count - 1)
            {
                DisplayQuestion(_currentIndex + 1);
            }
            else
            {
                MessageBox.Show("Bạn đã đến câu hỏi cuối cùng. Hãy kiểm tra lại các câu trả lời trước khi nộp bài.");
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn nộp bài không?", "Xác nhận nộp bài", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SubmitQuiz();
            }
        }

        private async void SubmitQuiz()
        {
            // Logic nộp bài sẽ được viết ở đây (tính điểm, lưu Firestore)
            MessageBox.Show("Đã nộp bài thành công! Hệ thống đang chấm điểm...");
            
            // Quay lại trang danh sách bài thi
            var studentWin = Window.GetWindow(this) as StudentMainWindow;
            if (studentWin != null)
            {
                studentWin.BtnQuiz_Click(null, null);
            }
        }
    }
}
