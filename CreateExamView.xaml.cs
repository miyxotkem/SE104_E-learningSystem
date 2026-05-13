using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using e_learning_app.Class;

namespace e_learning_app
{
    public partial class CreateExamView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private bool _isInitialized = false;
        private List<Course> _allCourses = new();
        private Course _selectedCourse = null;
        private Exam _existingExam = null;

        public CreateExamView(DatabaseManager dbManager, Exam existingExam = null)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _existingExam = existingExam;
            _isInitialized = true;
            UpdatePreview();
            Loaded += async (s, e) => await LoadCoursesAsync();
        }

        // ========== LOAD COURSES ==========
        private async Task LoadCoursesAsync()
        {
            try
            {
                if (_dbManager == null)
                {
                    CbClass.ItemsSource = new List<string> { "❌ DatabaseManager null" };
                    return;
                }

                _allCourses = await _dbManager.GetAllCoursesAsync();
                
                if (_allCourses == null || _allCourses.Count == 0)
                {
                    CbClass.ItemsSource = new List<string> { "❌ Không có lớp học nào" };
                    CbClass.IsEnabled = false;
                    return;
                }

                var displayList = new List<string>();
                foreach (var course in _allCourses)
                {
                    displayList.Add($"🎓 {course.Title} ({course.ClassName})");
                }
                
                CbClass.ItemsSource = displayList;
                _isInitialized = true;
                
                if (_existingExam != null)
                {
                    int index = _allCourses.FindIndex(c => c.Id == _existingExam.ClassId);
                    CbClass.SelectedIndex = index >= 0 ? index : 0;

                    TxtTitle.Text = _existingExam.Title;
                    TxtDescription.Text = _existingExam.Description;

                    if (_existingExam.Deadline.HasValue)
                    {
                        ChkHasDeadline.IsChecked = true;
                        PanelDeadline.Visibility = Visibility.Visible;
                        DpDeadlineDate.SelectedDate = _existingExam.Deadline.Value.Date;
                        string timeStr = _existingExam.Deadline.Value.ToLocalTime().ToString("HH:mm");
                        if (timeStr == "23:59" || timeStr == "12:00" || timeStr == "17:00" || timeStr == "00:00")
                            SelectComboBoxItemByContent(CbDeadlineTime, timeStr);
                        else
                            CbDeadlineTime.Text = timeStr;
                    }

                    SelectComboBoxItemByContent(CbTimeLimit, _existingExam.TimeLimitMinutes.ToString());
                    SelectComboBoxItemByContent(CbPassingScore, _existingExam.PassingScore.ToString() + "%");

                    if (ChkPublished != null) ChkPublished.IsChecked = _existingExam.IsPublished;
                    if (ChkAllowReview != null) ChkAllowReview.IsChecked = _existingExam.AllowReview;
                    if (ChkRandomize != null) ChkRandomize.IsChecked = _existingExam.RandomizeQuestions;
                    if (ChkShowScore != null) ChkShowScore.IsChecked = _existingExam.ShowScore;
                    if (ChkMultipleAttempts != null)
                    {
                        ChkMultipleAttempts.IsChecked = _existingExam.AllowMultipleAttempts;
                        if (TxtMaxAttempts != null) TxtMaxAttempts.Text = _existingExam.MaxAttempts.ToString();
                    }
                }
                else
                {
                    CbClass.SelectedIndex = 0;
                }
                
                UpdatePreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadCoursesAsync: {ex.Message}");
                CbClass.Items.Add($"❌ Lỗi: {ex.Message}");
            }
        }

        private void SelectComboBoxItemByContent(ComboBox cb, string contentPart)
        {
            if (cb == null) return;
            foreach (var item in cb.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Content != null && cbi.Content.ToString().Contains(contentPart))
                {
                    cb.SelectedItem = item;
                    return;
                }
            }
        }

        // ========== CLASS SELECTION ==========
        private void CbClass_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!_isInitialized || CbClass.SelectedIndex < 0 || CbClass.SelectedIndex >= _allCourses.Count)
                    return;

                _selectedCourse = _allCourses[CbClass.SelectedIndex];
                TxtClassInfo.Text = $"📌 {_selectedCourse.ClassName} | {_selectedCourse.Category} | {_selectedCourse.StudentCount} học sinh";

                UpdatePreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CbClass_SelectionChanged: {ex.Message}");
            }
        }

        // ========== UPDATE PREVIEW ==========
        private void UpdatePreview()
        {
            if (!_isInitialized) return;

            try
            {
                int questions = _existingExam?.TotalQuestions ?? 0;

                DateTime? deadline = null;
                if (ChkHasDeadline?.IsChecked == true && DpDeadlineDate.SelectedDate.HasValue)
                {
                    deadline = DpDeadlineDate.SelectedDate.Value.Date;
                    string timeStr = (CbDeadlineTime.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? CbDeadlineTime.Text;
                    if (TimeSpan.TryParse(timeStr, out TimeSpan ts)) deadline = deadline.Value.Add(ts);
                    else if (timeStr == "23:59") deadline = deadline.Value.AddHours(23).AddMinutes(59);

                    // Warning if in the past
                    if (TxtDeadlineWarning != null)
                        TxtDeadlineWarning.Visibility = deadline.Value < DateTime.Now ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (TxtDeadlineWarning != null)
                {
                    TxtDeadlineWarning.Visibility = Visibility.Collapsed;
                }

                var previewExam = new Exam
                {
                    Id = "preview_id",
                    Title = (TxtTitle != null && !string.IsNullOrWhiteSpace(TxtTitle.Text)) ? TxtTitle.Text : "Tên bài thi",
                    Description = (TxtDescription != null && !string.IsNullOrWhiteSpace(TxtDescription.Text)) ? TxtDescription.Text : "Mô tả bài thi...",
                    ClassName = _selectedCourse != null ? $"{_selectedCourse.ClassName} - {_selectedCourse.Title}" : "Tên lớp",
                    TimeLimitMinutes = int.TryParse((CbTimeLimit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "60", out int t) ? t : 60,
                    PassingScore = double.TryParse((CbPassingScore?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.TrimEnd('%') ?? "50", out double s) ? s : 50,
                    TotalQuestions = questions,
                    IsActive = true, // Force active for preview styling
                    IsPublished = ChkPublished?.IsChecked ?? false,
                    Deadline = deadline,
                    CreatedAt = DateTime.UtcNow
                };

                if (PreviewExamCard != null)
                {
                    PreviewExamCard.DataContext = null;
                    PreviewExamCard.DataContext = previewExam;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ UpdatePreview: {ex.Message}");
            }
        }

        // ========== EVENT HANDLERS ==========
        private void Sync_UI(object sender, TextChangedEventArgs e)
        {
            if (_isInitialized) UpdatePreview();
        }

        private void Sync_UI_Selection(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) UpdatePreview();
        }
        
        private void Sync_UI_Toggle(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) UpdatePreview();
        }

        private void Sync_UI_Date(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) UpdatePreview();
        }

        private void ChkMultipleAttempts_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || PanelMaxAttempts == null) return;
            
            bool allow = ChkMultipleAttempts.IsChecked ?? false;
            PanelMaxAttempts.Visibility = allow ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        private void ChkHasDeadline_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || PanelDeadline == null) return;
            PanelDeadline.Visibility = ChkHasDeadline.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        // ========== SUBMIT ==========
        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCourse == null)
            {
                CustomDialog.Show("⚠️ Vui lòng chọn lớp học!", "Lỗi", DialogType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                CustomDialog.Show("⚠️ Vui lòng nhập tên bài thi!", "Lỗi", DialogType.Warning);
                return;
            }

            try
            {
                int maxAtt = 1;
                if (ChkMultipleAttempts.IsChecked == true && int.TryParse(TxtMaxAttempts.Text, out int m))
                    maxAtt = m;

                DateTime? deadline = null;
                if (ChkHasDeadline.IsChecked == true)
                {
                    if (!DpDeadlineDate.SelectedDate.HasValue)
                    {
                        CustomDialog.Show("⚠️ Vui lòng chọn ngày hạn chót!", "Lỗi", DialogType.Warning);
                        return;
                    }

                    deadline = DpDeadlineDate.SelectedDate.Value.Date;
                    string timeStr = (CbDeadlineTime.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? CbDeadlineTime.Text;
                    if (TimeSpan.TryParse(timeStr, out TimeSpan ts))
                        deadline = deadline.Value.Add(ts);
                    else if (timeStr == "23:59")
                        deadline = deadline.Value.AddHours(23).AddMinutes(59).AddSeconds(59);

                    if (deadline.Value < DateTime.Now)
                    {
                        CustomDialog.Show("⚠️ Hạn chót không thể ở trong quá khứ!", "Lỗi", DialogType.Warning);
                        return;
                    }
                    deadline = deadline.Value.ToUniversalTime();
                }

                var newExam = new Exam
                {
                    Id = _existingExam != null ? _existingExam.Id : Guid.NewGuid().ToString("N"),
                    ClassId = _selectedCourse.Id,
                    ClassName = $"{_selectedCourse.ClassName} - {_selectedCourse.Title}",
                    Title = TxtTitle.Text.Trim(),
                    Description = TxtDescription.Text.Trim(),
                    Deadline = deadline,
                    TimeLimitMinutes = int.Parse((CbTimeLimit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "60"),
                    PassingScore = double.Parse((CbPassingScore?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.TrimEnd('%') ?? "50"),
                    TotalQuestions = _existingExam?.TotalQuestions ?? 0,
                    IsActive = true,
                    IsPublished = ChkPublished?.IsChecked ?? false,
                    AllowReview = ChkAllowReview?.IsChecked ?? true,
                    RandomizeQuestions = ChkRandomize?.IsChecked ?? false,
                    ShowScore = ChkShowScore?.IsChecked ?? true,
                    AllowMultipleAttempts = ChkMultipleAttempts?.IsChecked ?? true,
                    MaxAttempts = maxAtt,
                    QuestionIds = _existingExam != null ? _existingExam.QuestionIds : new List<string>(),
                    CreatedAt = _existingExam != null ? _existingExam.CreatedAt : DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (Window.GetWindow(this) is MainWindow mw)
                {
                    mw.NavigateTo(new e_learning_app.CreateExamQuestionsView(_dbManager, newExam));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Submit Error: {ex.Message}");
                CustomDialog.Show($"❌ Lỗi: {ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        // ========== NAVIGATION ==========
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => NavigateBack();
        private void BtnBack_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.NavigateTo(new ExamManagementView(_dbManager));
            }
        }
    }
}