using e_learning_app;
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
        private ExamDraft _draft; // non-null when student has a saved draft

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
                var historyRes = await e_learning_app.Class.ApiService.GetAsync<System.Collections.Generic.List<e_learning_app.Class.ExamSubmissionResponse>>("exams/my-history");
                _submissions = historyRes != null ? historyRes.Where(h => h.Data?.ExamId == _exam.Id).Select(h => h.Data).OrderByDescending(s => s.SubmittedAt).ToList() : new System.Collections.Generic.List<e_learning_app.Class.ExamSubmission>();

                double totalPoints = _exam.TotalQuestions > 0 ? _exam.TotalQuestions : 10;
                var detailRes = await e_learning_app.Class.ApiService.GetAsync<System.Text.Json.JsonElement?>($"exams/{_exam.Id}");
                if (detailRes != null && detailRes.HasValue)
                {
                    try
                    {
                        if (detailRes.Value.TryGetProperty("Data", out var docData) || detailRes.Value.TryGetProperty("data", out docData))
                        {
                            if ((docData.TryGetProperty("Questions", out var questionsElem) || docData.TryGetProperty("questions", out questionsElem)) && questionsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                totalPoints = 0;
                                foreach (var qElem in questionsElem.EnumerateArray())
                                {
                                    if (qElem.TryGetProperty("Points", out var ptsElem)) totalPoints += ptsElem.GetDouble();
                                }
                            }
                        }
                    }
                    catch { }
                }
                if (totalPoints == 0) totalPoints = _exam.TotalQuestions > 0 ? _exam.TotalQuestions : 10;

                // Highest Score Calculation (Hệ 10)
                double maxPercentage = _submissions.Any() ? _submissions.Max(s => s.Percentage) : 0;
                double maxScoreValue = maxPercentage / 10;
                TxtHighestScore.Text = _exam.ShowScore ? $"{maxScoreValue:F1} / 10" : "---";

                // Check attempts limit
                bool isLimitReached = false;
                if (!_exam.AllowMultipleAttempts)
                {
                    if (_submissions.Count >= 1) isLimitReached = true;
                }
                else
                {
                    if (_submissions.Count >= _exam.MaxAttempts) isLimitReached = true;
                }

                if (!_exam.IsActive)
                {
                    BtnStartQuiz.IsEnabled = false;
                    BtnStartQuiz.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                    BtnStartQuiz.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    BtnStartQuiz.Content = "🔒 Bài thi đang bị khóa";
                }
                else if (_exam.Deadline.HasValue && DateTime.Now > _exam.Deadline.Value)
                {
                    BtnStartQuiz.IsEnabled = false;
                    BtnStartQuiz.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                    BtnStartQuiz.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    BtnStartQuiz.Content = "⌛ Đã quá hạn làm bài";
                }
                else if (isLimitReached)
                {
                    BtnStartQuiz.IsEnabled = false;
                    BtnStartQuiz.Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                    BtnStartQuiz.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    BtnStartQuiz.Content = "❌ Hết lượt làm bài";
                }

                // Map to ViewModel-like structure for display
                var displayList = _submissions.Select(s => new
                {
                    Submission = s, // Keep reference for click
                    s.SubmittedAt,
                    ScoreDisplay = _exam.ShowScore ? $"{s.Score:F1} / 10" : "Đã nộp",
                    TimeDisplay = $"Thời gian làm bài: {TimeSpan.FromSeconds(s.TimeSpentSeconds):mm\\:ss}",
                    StatusText = !_exam.ShowScore ? "---" : (s.Percentage >= _exam.PassingScore ? "Đạt" : "Không đạt"),
                    StatusBrush = !_exam.ShowScore ? Brushes.Gray : (s.Percentage >= _exam.PassingScore ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)))
                }).ToList();

                ItemsHistory.ItemsSource = displayList;

                // --- Check for a saved draft ---
                _draft = await _dbManager.GetExamDraftAsync(_exam.Id, user.Id);
                if (_draft != null)
                {
                    double elapsed = (DateTime.UtcNow - _draft.StartedAt).TotalSeconds;
                    double leftSec = (_exam.TimeLimitMinutes * 60.0) - elapsed;

                    if (leftSec <= 0)
                    {
                        // Time already expired — silently clean up the stale draft
                        await _dbManager.DeleteExamDraftAsync(_exam.Id, user.Id);
                        _draft = null;
                    }
                    else
                    {
                        string timeLeftStr = TimeSpan.FromSeconds(leftSec).ToString(
                            leftSec >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");

                        DraftBanner.Visibility      = Visibility.Visible;
                        TxtDraftTimeLeft.Text       = timeLeftStr;
                        TxtDraftAnswerCount.Text    = $"Đã trả lời {_draft.Answers?.Count ?? 0} / {_exam.TotalQuestions} câu";
                        TxtDraftSavedAt.Text        = $"Lưu lúc: {_draft.SavedAt.ToLocalTime():dd/MM/yyyy HH:mm}";

                        // Update start button label so it's clear
                        if (BtnStartQuiz.IsEnabled)
                            BtnStartQuiz.Content = "🔄 Bắt đầu làm mới";
                    }
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"Lỗi tải lịch sử: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        private void AttemptCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                if (!_exam.AllowReview)
                {
                    CustomDialog.Show("Giảng viên đã tắt tính nang xem lại bài làm cho bài thi này.", "Thông báo", DialogType.Info);
                    return;
                }

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

        private void BtnContinueDraft_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to TakeQuizView — it will detect the draft and offer to resume
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new TakeQuizView(_dbManager, _exam);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new TakeQuizView(_dbManager, _exam);
        }

        private async void BtnDiscardDraft_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = CustomDialog.Confirm(
                "Bạn có chắc muốn xóa bài làm đang dở?\nHành động này không thể hoàn tác.",
                "Xóa bài dở", "Xóa", "Hủy", DialogType.Warning);
            if (!confirmed) return;

            var user = _dbManager.GetCurrentUser();
            if (user == null) return;

            await _dbManager.DeleteExamDraftAsync(_exam.Id, user.Id);
            _draft = null;
            DraftBanner.Visibility = Visibility.Collapsed;

            // Reset start button label
            if (BtnStartQuiz.IsEnabled)
                BtnStartQuiz.Content = "🚀 Bắt đầu làm bài";
        }

        private void BtnStartQuiz_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.MainContentArea.Content = new TakeQuizView(_dbManager, _exam);
            else if (Window.GetWindow(this) is StudentMainWindow smw)
                smw.StudentContentArea.Content = new TakeQuizView(_dbManager, _exam);
        }
        private void ItemsHistory_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}
