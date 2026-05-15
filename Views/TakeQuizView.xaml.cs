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
        private DateTime _startTime;   // Real UTC start time used by timer
        private ExamDraft _activeDraft; // Non-null when we restored from a saved draft

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

                    // Check for a saved draft
                    _activeDraft = await _dbManager.GetExamDraftAsync(_exam.Id, user.Id);
                }

                // Load questions from Firestore
                _questions = await _dbManager.GetExamQuestionsAsync(_exam.Id);

                if (_questions == null || _questions.Count == 0)
                {
                    CustomDialog.Show("Không tìm thấy câu hỏi cho bài thi này.", "Thông báo", DialogType.Info);
                    return;
                }

                if (_activeDraft != null)
                {
                    // --- Restore from draft ---
                    // Compute how much time has truly elapsed since the student first started
                    double elapsedSeconds = (DateTime.UtcNow - _activeDraft.StartedAt).TotalSeconds;
                    double leftSeconds = _totalSeconds - elapsedSeconds;

                    if (leftSeconds <= 0)
                    {
                        // Time already expired while they were away - auto-submit
                        _startTime = _activeDraft.StartedAt.ToLocalTime();
                        RestoreDraftState(_activeDraft);
                        CustomDialog.Show("Thời gian làm bài đã hết! Hệ thống sẽ tự động nộp bài.", "Hết giờ", DialogType.Warning);
                        await SubmitQuiz();
                        return;
                    }

                    bool resume = CustomDialog.Confirm(
                        $"Bạn có bài làm đang dở của bài thi này.\n" +
                        $"Thời gian còn lại: {TimeSpan.FromSeconds(leftSeconds):mm\\:ss}\n\n" +
                        "Tiếp tục làm bài?",
                        "Tiếp tục bài làm", "Tiếp tục", "Bắt đầu lại", DialogType.Question);

                    if (resume)
                    {
                        // Keep original startTime so timer reflects real elapsed time
                        _startTime = _activeDraft.StartedAt.ToLocalTime();
                        _remainingTime = TimeSpan.FromSeconds(leftSeconds);
                        RestoreDraftState(_activeDraft);
                    }
                    else
                    {
                        // Delete old draft, fresh start
                        var u = _dbManager.GetCurrentUser();
                        if (u != null) await _dbManager.DeleteExamDraftAsync(_exam.Id, u.Id);
                        _activeDraft = null;
                        FreshStart();
                    }
                }
                else
                {
                    FreshStart();
                }

                // Initialize timer
                StartTimer();

                // Render current question
                ShowQuestion(_currentIndex);
                UpdateQuestionMap();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi tải bài thi: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        private void FreshStart()
        {
            if (_exam.RandomizeQuestions)
            {
                var rnd = new Random();
                _questions = _questions.OrderBy(x => rnd.Next()).ToList();
            }
            _startTime = DateTime.Now;
            _remainingTime = TimeSpan.FromSeconds(_totalSeconds);
            _currentIndex = 0;
        }

        private void RestoreDraftState(ExamDraft draft)
        {
            // Restore answers
            _studentAnswers = new Dictionary<string, string>(draft.Answers ?? new());
            // Restore marked-for-review
            _markedForReview = new HashSet<string>(draft.MarkedForReview ?? new List<string>());
            // Restore last viewed question index (clamped)
            _currentIndex = Math.Max(0, Math.Min(draft.LastQuestionIndex, _questions.Count - 1));
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

        private async void BtnSaveExit_Click(object sender, RoutedEventArgs e)
        {
            var user = _dbManager.GetCurrentUser();
            if (user == null)
            {
                CustomDialog.Show("Không xác định được học sinh hiện tại.", "Lỗi", DialogType.Error);
                return;
            }

            // Stop the UI timer (timer "keeps running" conceptually via startTime saved in draft)
            _timer?.Stop();

            var draft = new ExamDraft
            {
                ExamId        = _exam.Id,
                StudentId     = user.Id,
                StudentName   = user.FullName,
                // Preserve the ORIGINAL start time so elapsed time accumulates correctly
                StartedAt     = (_activeDraft != null)
                                    ? _activeDraft.StartedAt          // already UTC
                                    : _startTime.ToUniversalTime(),
                Answers       = new Dictionary<string, string>(_studentAnswers),
                MarkedForReview = new List<string>(_markedForReview),
                LastQuestionIndex = _currentIndex
            };

            bool saved = await _dbManager.SaveExamDraftAsync(draft);

            if (saved)
            {
                // Show how much time they have left when they return
                double elapsedSec  = (DateTime.UtcNow - draft.StartedAt).TotalSeconds;
                double leftSec     = Math.Max(0, _totalSeconds - elapsedSec);
                string leftDisplay = TimeSpan.FromSeconds(leftSec).ToString(@"mm\:ss");

                CustomDialog.Show(
                    $"Bài làm đã được lưu lại!\n" +
                    $"⚠️ Đồng hồ vẫn tiếp tục chạy.\n" +
                    $"Thời gian còn lại khi quay lại: ~{leftDisplay}",
                    "Lưu thành công", DialogType.Success);
            }
            else
            {
                CustomDialog.Show("Không thể lưu bài làm. Kiểm tra kết nối mạng!", "Lỗi lưu", DialogType.Error);
                // Resume timer if save failed
                _timer?.Start();
                return;
            }

            // Navigate away
            if (Window.GetWindow(this) is MainWindow mainWin)
                mainWin.NavDashboard_Click(null, null);
            else if (Window.GetWindow(this) is StudentMainWindow stuWin)
                stuWin.StudentContentArea.Content = new StudentDashboardView(_dbManager);
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

            // If we restored from a draft, measure elapsed from the ORIGINAL startTime
            DateTime effectiveStart = (_activeDraft != null)
                ? _activeDraft.StartedAt.ToLocalTime()
                : _startTime;

            int timeSpent = (int)(DateTime.Now - effectiveStart).TotalSeconds;
            if (_totalSeconds > 0 && timeSpent > _totalSeconds)
                timeSpent = (int)_totalSeconds;

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
                    // Delete draft (if any) now that we have a real submission
                    var u = _dbManager.GetCurrentUser();
                    if (u != null)
                        await _dbManager.DeleteExamDraftAsync(_exam.Id, u.Id);

                    string msg = "Nộp bài thành công!";
                    if (_exam.ShowScore)
                        msg += $"\nĐiểm của bạn: {graded.Score:F1} / 10 ({graded.Percentage:F1}%)";
                    else
                        msg += "\nKết quả của bạn đã được ghi nhận.";

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