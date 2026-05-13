using e_learning_app;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Animation;
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

                // Double check attempt limit
                var user = _dbManager.GetCurrentUser();
                if (user != null)
                {
                    var subSnap = await _dbManager.GetDb.Collection("exam_submissions")
                        .WhereEqualTo("ExamId", _exam.Id)
                        .WhereEqualTo("StudentId", user.Id)
                        .GetSnapshotAsync();

                    int attemptCount = subSnap.Count;
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

                // Load questions from Firestore
                _questions = await _dbManager.GetExamQuestionsAsync(_exam.Id);

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
                else if (_remainingTime.TotalMinutes < 5)
                {
                    // Nguy hiểm: < 5 phút -> Đỏ và nhấp nháy
                    TxtTimer.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red-500
                    TimeProgressFill.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    TxtTimer.Opacity = TxtTimer.Opacity >= 0.9 ? 0.5 : 1.0;
                }
                else if (_remainingTime.TotalMinutes < 10)
                {
                    // Cảnh báo: < 10 phút -> Vàng cam
                    TxtTimer.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Amber-500
                    TimeProgressFill.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    TxtTimer.Opacity = 1.0;
                }
                else
                {
                    // Bình thường
                    TxtTimer.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                    TimeProgressFill.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                    TxtTimer.Opacity = 1.0;
                }
            };
            _timer.Start();
        }

        private void ShowQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count) return;

            _currentIndex = index;
            var q = _questions[index];

            // Trigger Animation (Slide & Fade)
            if (MainContentContainer != null)
            {
                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
                var slideAnim = new ThicknessAnimation(new Thickness(40, 52, 40, 0), new Thickness(40, 32, 40, 20), TimeSpan.FromMilliseconds(250));
                slideAnim.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                MainContentContainer.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                MainContentContainer.BeginAnimation(FrameworkElement.MarginProperty, slideAnim);
            }

            // Update UI
            TxtQuestionBadge.Text = $"Câu {index + 1}";
            TxtQuestionContent.Text = q.Content;
            TxtProgressHeader.Text = $"Câu {index + 1} / {_questions.Count}  ·  {q.Points} điểm";

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
            BtnMarkReview.IsChecked = _markedForReview.Contains(q.Id);

            // Buttons
            BtnPrev.IsEnabled = index > 0;
            BtnNext.Content = index == _questions.Count - 1 ? "Câu cuối" : "Câu tiếp theo →";

            UpdateStats();
            UpdateQuestionMap(); // Cập nhật lại bản đồ để làm nổi bật câu hiện tại
        }

        private void UpdateQuestionMap()
        {
            if (PanelQuestionMap.Children.Count != _questions.Count)
            {
                PanelQuestionMap.Children.Clear();
                for (int i = 0; i < _questions.Count; i++)
                {
                    var btn = new Button
                    {
                        Style = (Style)Resources["QMapBtn"],
                        Content = (i + 1).ToString(),
                        Tag = i,
                        FontWeight = FontWeights.Bold
                    };
                    btn.Click += (s, e) => ShowQuestion((int)(s as Button).Tag);
                    PanelQuestionMap.Children.Add(btn);
                }
            }

            for (int i = 0; i < _questions.Count; i++)
            {
                var q = _questions[i];
                var btn = (Button)PanelQuestionMap.Children[i];

                bool isCurrent = i == _currentIndex;
                bool isAnswered = _studentAnswers.ContainsKey(q.Id);
                bool isMarked = _markedForReview.Contains(q.Id);

                // 1. Màu sắc nền và chữ dựa trên trạng thái (Đã làm / Xem lại)
                if (isMarked && isAnswered)
                {
                    // Đã làm nhưng đánh dấu xem lại
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)); // Nền xanh (Đã làm)
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Chữ cam (Xem lại)
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Viền cam
                }
                else if (isMarked && !isAnswered)
                {
                    // Chưa làm và đánh dấu xem lại
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xC3)); // Nền vàng
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)); // Chữ cam đậm
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Viền cam
                }
                else if (!isMarked && isAnswered)
                {
                    // Đã làm
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)); // Nền xanh
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)); // Chữ xanh
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)); // Viền xanh
                }
                else
                {
                    // Chưa làm
                    btn.Background = Brushes.White;
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)); // Chữ xám
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)); // Viền xám
                }

                // 2. Logic làm nổi bật câu hiện tại (Current Selection)
                if (isCurrent)
                {
                    // Phóng to, viền dày màu xanh dương, thêm đổ bóng
                    btn.Width = 46; 
                    btn.Height = 46;
                    btn.BorderThickness = new Thickness(2.5);
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)); // Viền xanh dương đậm (Blue-600)
                    btn.Effect = new System.Windows.Media.Effects.DropShadowEffect 
                    { 
                        BlurRadius = 8, 
                        ShadowDepth = 2, 
                        Opacity = 0.25, 
                        Color = Colors.Black 
                    };
                }
                else
                {
                    // Trạng thái bình thường
                    btn.Width = 40; 
                    btn.Height = 40;
                    btn.BorderThickness = new Thickness(1);
                    btn.Effect = null;
                }
            }
        }

        private void BtnToggleMap_Click(object sender, RoutedEventArgs e)
        {
            if (ColumnRightPanel.Width.Value > 0)
            {
                // Đang mở -> Đóng lại
                ColumnRightPanel.Width = new GridLength(0);
                if (BtnFloatingToggle != null)
                {
                    BtnFloatingToggle.Content = "⯇";
                    BtnFloatingToggle.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                    BtnFloatingToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                    if (BtnFloatingToggle.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
                        shadow.Opacity = 0.15;
                }
            }
            else
            {
                // Đang đóng -> Mở ra
                ColumnRightPanel.Width = new GridLength(280);
                if (BtnFloatingToggle != null)
                {
                    BtnFloatingToggle.Content = "⯈";
                    BtnFloatingToggle.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                    BtnFloatingToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    if (BtnFloatingToggle.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
                        shadow.Opacity = 0;
                }
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

        private void BtnMarkReview_Click(object sender, RoutedEventArgs e)
        {
            var qId = _questions[_currentIndex].Id;
            if (_markedForReview.Contains(qId))
            {
                _markedForReview.Remove(qId);
            }
            else
            {
                _markedForReview.Add(qId);
            }

            UpdateQuestionMap();
            UpdateStats();
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

            int timeSpent = (int)(DateTime.Now - _startTime).TotalSeconds;
            if (_totalSeconds > 0 && timeSpent > _totalSeconds)
            {
                timeSpent = (int)_totalSeconds;
            }

            var user = _dbManager.GetCurrentUser();
            var submission = new ExamSubmission
            {
                ExamId = _exam.Id,
                StudentId = user.Id,
                StudentName = user.FullName,
                TimeSpentSeconds = timeSpent,
                Status = SubmissionStatus.Submitted
            };

            foreach (var q in _questions)
            {
                submission.Answers.Add(new AnswerResponse
                {
                    QuestionId = q.Id,
                    QuestionOrder = q.QuestionOrder,
                    StudentAnswer = _studentAnswers.ContainsKey(q.Id) ? _studentAnswers[q.Id] : ""
                });
            }

            try
            {
                // Grade automatically if multiple choice
                var graded = await _dbManager.AutoGradeAndSubmitExamAsync(submission);

                if (graded != null)
                {
                    string msg = "Nộp bài thành công!";
                    if (_exam.ShowScore)
                    {
                        msg += $"\nĐiểm của bạn: {graded.Score:F1} / 10 ({graded.Percentage:F1}%)";
                    }
                    else
                    {
                        msg += "\nKết quả của bạn đã được ghi nhận.";
                    }

                    CustomDialog.Show(msg, "Kết quả", DialogType.Success);

                    // Navigate back to student dashboard or quiz list
                    if (Window.GetWindow(this) is MainWindow mw)
                        mw.NavDashboard_Click(null, null);
                    else if (Window.GetWindow(this) is StudentMainWindow smw)
                        smw.StudentContentArea.Content = new StudentDashboardView(_dbManager);
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi nộp bài: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }
    }
}