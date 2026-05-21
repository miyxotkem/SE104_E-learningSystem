using e_learning_app;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class TakeQuizView : UserControl
    {
        private Exam _exam;
        private DatabaseManager _dbManager;
        private List<ExamQuestion> _questions = new();
        private Dictionary<string, string> _studentAnswers = new(); // QuestionId -> AnswerIndex/Text
        private HashSet<string> _markedForReview = new();

        private int _currentIndex = 0;
        private DispatcherTimer _timer;
        private TimeSpan _remainingTime;
        private double _totalSeconds;
        private DateTime _startTime;

        public TakeQuizView(DatabaseManager dbManager, Exam exam)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _exam = exam;

            _startTime = DateTime.Now;
            _totalSeconds = _exam.TimeLimitMinutes * 60;
            _remainingTime = TimeSpan.FromSeconds(_totalSeconds);

            Loaded += TakeQuizView_Loaded;
        }

        private async void TakeQuizView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtQuizTitle.Text = $"📝  {_exam.Title}";

                // Double check attempt limit qua API
                var user = _dbManager.GetCurrentUser();
                if (user != null)
                {
                    try
                    {
                        var history = await e_learning_app.Class.ApiService.GetAsync<List<System.Text.Json.JsonElement>>("exams/my-history");
                        int attemptCount = history?.Count(h =>
                        {
                            if (h.TryGetProperty("Data", out var d) && d.TryGetProperty("ExamId", out var eid))
                                return eid.GetString() == _exam.Id;
                            return false;
                        }) ?? 0;

                        int limit = _exam.AllowMultipleAttempts ? _exam.MaxAttempts : 1;

                        if (attemptCount >= limit)
                        {
                            CustomDialog.Show("Bạn đã hết lượt làm bài cho bài thi này!", "Thông báo", DialogType.Warning);
                            if (Window.GetWindow(this) is MainWindow mw)
                                mw.MainContentArea.Content = new QuizHistoryView(_dbManager, _exam);
                            else if (Window.GetWindow(this) is StudentMainWindow smw)
                                smw.StudentContentArea.Content = new QuizHistoryView(_dbManager, _exam);
                            return;
                        }
                    }
                    catch
                    {
                        // Nếu API lỗi, vẫn cho tiếp tục
                    }
                }

                // Load questions from Firestore
                var detailRes = await e_learning_app.Class.ApiService.GetAsync<System.Text.Json.JsonElement?>($"exams/{_exam.Id}");
                if (detailRes != null && detailRes.HasValue)
                {
                    _questions = new System.Collections.Generic.List<e_learning_app.Class.ExamQuestion>();
                    try
                    {
                        if (detailRes.Value.TryGetProperty("Data", out var docData) || detailRes.Value.TryGetProperty("data", out docData))
                        {
                            if ((docData.TryGetProperty("Questions", out var questionsElem) || docData.TryGetProperty("questions", out questionsElem)) && questionsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                int qIndex = 0;
                                foreach (var qElem in questionsElem.EnumerateArray())
                                {
                                    var q = new e_learning_app.Class.ExamQuestion { Id = qIndex.ToString(), QuestionOrder = qIndex + 1 };
                                    if (qElem.TryGetProperty("QuestionText", out var textElem) || qElem.TryGetProperty("questionText", out textElem)) q.Content = textElem.GetString();
                                    if (qElem.TryGetProperty("CorrectOptionIndex", out var correctIdxElem) || qElem.TryGetProperty("correctOptionIndex", out correctIdxElem)) q.CorrectAnswerIndex = correctIdxElem.GetInt32();
                                    if (qElem.TryGetProperty("Points", out var pointsElem) || qElem.TryGetProperty("points", out pointsElem)) q.Points = pointsElem.GetDouble();

                                    if ((qElem.TryGetProperty("Options", out var optsElem) || qElem.TryGetProperty("options", out optsElem)) && optsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        var opts = optsElem.EnumerateArray().Select(o => o.GetString()).ToList();
                                        q.Options = opts;
                                    }
                                    _questions.Add(q);
                                    qIndex++;
                                }
                            }
                        }
                    }
                    catch (System.Exception ex) 
                    { 
                        System.Windows.MessageBox.Show($"Lỗi phân tích câu hỏi từ máy chủ:\n{ex.Message}\n\nKiểm tra lại dữ liệu trong bài thi.", "Lỗi dữ liệu JSON", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        System.Console.WriteLine("Parse questions error: " + ex.Message); 
                    }
                }

                if (_questions == null || _questions.Count == 0)
                {
                    CustomDialog.Show("Không tìm thấy câu hỏi cho bài thi này.", "Thông báo", DialogType.Info);
                    return;
                }

                if (_exam.RandomizeQuestions)
                {
                    var rnd = new Random();
                    _questions = _questions.OrderBy(x => rnd.Next()).ToList();
                }

                // Initialize timer
                StartTimer();

                // Render first question
                ShowQuestion(0);
                UpdateQuestionMap();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi tải bài thi: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));

                // Formatting: hh:mm:ss if > 1 hour, otherwise mm:ss
                if (_remainingTime.TotalHours >= 1)
                    TxtTimer.Text = _remainingTime.ToString(@"hh\:mm\:ss");
                else
                    TxtTimer.Text = _remainingTime.ToString(@"mm\:ss");

                // Update Time Progress Bar
                double percent = _remainingTime.TotalSeconds / _totalSeconds;
                TimeProgressFill.Width = 140 * percent;

                if (_remainingTime.TotalSeconds <= 0)
                {
                    _timer.Stop();
                    TimeProgressFill.Width = 0;
                    CustomDialog.Show("Hết giờ làm bài! Hệ thống sẽ tự động nộp bài.", "Hết giờ", DialogType.Warning);
                    _ = SubmitQuiz(); // Use discard for async call in non-async event
                }
                else if (_remainingTime.TotalMinutes < 1)
                {
                    // Pulse Red when < 1 min
                    TxtTimer.Foreground = Brushes.Red;
                    TimeProgressFill.Background = Brushes.Red;
                    TxtTimer.Opacity = TxtTimer.Opacity == 1 ? 0.6 : 1;
                }
                else if (_remainingTime.TotalMinutes < 5)
                {
                    TxtTimer.Foreground = Brushes.OrangeRed;
                    TimeProgressFill.Background = Brushes.OrangeRed;
                }
            };
            _timer.Start();
        }

        private void ShowQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count) return;

            _currentIndex = index;
            var q = _questions[index];

            // Update UI
            TxtQuestionBadge.Text = $"Câu {index + 1}";
            TxtQuestionContent.Text = q.Content;
            TxtProgressHeader.Text = $"Câu {index + 1} / {_questions.Count}  ·  {q.Points} diểm";

            // Progress Bar
            double progress = (double)(index + 1) / _questions.Count * 180;
            ProgressBarFill.Width = progress;
            TxtProgressFooter.Text = $"{index + 1}/{_questions.Count} câu";

            // Hint
            // (Assuming ExamQuestion doesn't have a Hint field in current model, but let's hide it)
            BorderHint.Visibility = Visibility.Collapsed;

            // Answers
            PanelAnswers.Children.Clear();
            if (q.Type == QuestionType.MultipleChoice || q.Type == QuestionType.TrueFalse)
            {
                for (int i = 0; i < q.Options.Count; i++)
                {
                    var rb = new RadioButton
                    {
                        Style = (Style)Resources["AnswerRadioBtn"],
                        GroupName = $"Q_{q.Id}",
                        Tag = i.ToString(),
                        IsChecked = _studentAnswers.ContainsKey(q.Id) && _studentAnswers[q.Id] == i.ToString()
                    };

                    var tb = new TextBlock { FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) };
                    tb.Inlines.Add(new System.Windows.Documents.Run { Text = $"{(char)('A' + i)}. ", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)) });
                    tb.Inlines.Add(new System.Windows.Documents.Run { Text = q.Options[i] });

                    rb.Content = tb;
                    rb.Checked += (s, e) => {
                        _studentAnswers[q.Id] = (s as RadioButton).Tag.ToString();
                        UpdateQuestionMap();
                        UpdateStats();
                    };

                    PanelAnswers.Children.Add(rb);
                }
            }

            // Mark Review state
            if (_markedForReview.Contains(q.Id))
                BtnMarkReview.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xC3)); // Yellowish
            else
                BtnMarkReview.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xF0));

            // Buttons
            BtnPrev.IsEnabled = index > 0;
            BtnNext.Content = index == _questions.Count - 1 ? "Câu cuối" : "Câu tiếp theo →";

            UpdateStats();
        }

        private void UpdateQuestionMap()
        {
            PanelQuestionMap.Children.Clear();
            for (int i = 0; i < _questions.Count; i++)
            {
                var q = _questions[i];
                var btn = new Button
                {
                    Style = (Style)Resources["QMapBtn"],
                    Content = (i + 1).ToString(),
                    Tag = i,
                    FontWeight = FontWeights.Bold
                };

                // Colors based on state
                if (i == _currentIndex)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                    btn.Foreground = Brushes.White;
                    btn.Width = 38; btn.Height = 38; // Current is slightly bigger
                }
                else if (_markedForReview.Contains(q.Id))
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                }
                else if (_studentAnswers.ContainsKey(q.Id))
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                }

                btn.Click += (s, e) => ShowQuestion((int)(s as Button).Tag);
                PanelQuestionMap.Children.Add(btn);
            }
        }

        private void UpdateStats()
        {
            int done = _studentAnswers.Count;
            int review = _markedForReview.Count;
            int todo = _questions.Count - done;

            TxtStatsDone.Text = $"{done} / {_questions.Count}";
            TxtStatsReview.Text = $"{review} câu";
            TxtStatsTodo.Text = $"{todo} câu";
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => ShowQuestion(_currentIndex - 1);

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _questions.Count - 1)
                ShowQuestion(_currentIndex + 1);
        }

        private void BtnMarkReview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var qId = _questions[_currentIndex].Id;
            if (_markedForReview.Contains(qId))
                _markedForReview.Remove(qId);
            else
                _markedForReview.Add(qId);

            ShowQuestion(_currentIndex); // Refresh UI
            UpdateQuestionMap();
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = CustomDialog.Confirm("Bạn có chắc chắn muốn nộp bài?", "Xác nhận nộp bài", "Nộp bài", "Hủy", DialogType.Question);
            if (confirmed)
            {
                await SubmitQuiz();
            }
        }

        private async System.Threading.Tasks.Task SubmitQuiz()
        {
            _timer?.Stop();

            var request = new e_learning_app.Class.SubmitExamRequest();
            for (int i = 0; i < _questions.Count; i++)
            {
                var q = _questions[i];
                if (_studentAnswers.ContainsKey(q.Id) && int.TryParse(_studentAnswers[q.Id], out int ansIdx))
                {
                    // The backend API expects the key to be the question index as a string
                    request.Answers.Add(i.ToString(), ansIdx);
                }
            }

            try
            {
                var response = await e_learning_app.Class.ApiService.PostAsync<e_learning_app.Class.SubmitExamRequest, System.Text.Json.JsonElement?>($"exams/{_exam.Id}/submit", request);

                if (response != null && response.HasValue)
                {
                    string msg = "Nộp bài thành công!";
                    if (_exam.ShowScore)
                    {
                        int score = 0;
                        int totalQuestions = _questions.Count;
                        
                        try
                        {
                            var dataElement = response.Value;
                            if (dataElement.TryGetProperty("Score", out var scoreProp) || dataElement.TryGetProperty("score", out scoreProp)) score = scoreProp.GetInt32();
                            if (dataElement.TryGetProperty("TotalQuestions", out var totalQProp) || dataElement.TryGetProperty("totalQuestions", out totalQProp)) totalQuestions = totalQProp.GetInt32();
                        }
                        catch { }

                        double percentage = totalQuestions > 0 ? (double)score / totalQuestions * 100 : 0;
                        msg += $"\nĐiểm của bạn: {score} / {totalQuestions} ({percentage:F1}%)";
                    }
                    else
                    {
                        msg += "\nKết quả của bạn đã được ghi nhận.";
                    }

                    CustomDialog.Show(msg, "Kết quả", DialogType.Success);

                    if (Window.GetWindow(this) is MainWindow mw)
                        mw.NavDashboard_Click(null, null);
                    else if (Window.GetWindow(this) is StudentMainWindow smw)
                        smw.NavigateTo(new Views.StudentQuizView(_dbManager));
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi nộp bài: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }
    }
}
