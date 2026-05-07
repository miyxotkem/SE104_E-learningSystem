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
                    TxtTotalQuestions.Text = _existingExam.TotalQuestions.ToString();

                    SelectComboBoxItemByContent(CbTimeLimit, _existingExam.TimeLimitMinutes.ToString());
                    SelectComboBoxItemByContent(CbPassingScore, _existingExam.PassingScore.ToString() + "%");

                    if (ChkPublished != null) ChkPublished.IsChecked = _existingExam.IsPublished;
                    if (ChkAllowReview != null) ChkAllowReview.IsChecked = _existingExam.AllowReview;
                    if (ChkRandomize != null) ChkRandomize.IsChecked = _existingExam.RandomizeQuestions;
                    if (ChkShowScore != null) ChkShowScore.IsChecked = _existingExam.ShowScore;
                    if (ChkMultipleAttempts != null) ChkMultipleAttempts.IsChecked = _existingExam.AllowMultipleAttempts;
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
                int questions = 0;
                if (TxtTotalQuestions != null && int.TryParse(TxtTotalQuestions.Text, out int q))
                    questions = q;

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

        // ========== SUBMIT ==========
        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCourse == null)
            {
                MessageBox.Show("⚠️ Vui lòng chọn lớp học!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                MessageBox.Show("⚠️ Vui lòng nhập tên bài thi!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newExam = new Exam
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClassId = _selectedCourse.Id,
                    ClassName = $"{_selectedCourse.ClassName} - {_selectedCourse.Title}",
                    Title = TxtTitle.Text.Trim(),
                    Description = TxtDescription.Text.Trim(),
                    TimeLimitMinutes = int.Parse((CbTimeLimit?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "60"),
                    PassingScore = double.Parse((CbPassingScore?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.TrimEnd('%') ?? "50"),
                    TotalQuestions = int.Parse(TxtTotalQuestions.Text ?? "0"),
                    IsActive = true,
                    IsPublished = ChkPublished?.IsChecked ?? false,
                    AllowReview = ChkAllowReview?.IsChecked ?? true,
                    RandomizeQuestions = ChkRandomize?.IsChecked ?? false,
                    ShowScore = ChkShowScore?.IsChecked ?? true,
                    AllowMultipleAttempts = ChkMultipleAttempts?.IsChecked ?? true,
                    MaxAttempts = 3,
                    QuestionIds = new List<string>(),
                    CreatedAt = DateTime.UtcNow,
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
                MessageBox.Show($"❌ Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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