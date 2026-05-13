using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using e_learning_app.Class;

namespace e_learning_app
{
    public partial class ExamManagementView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private List<Exam> _allExams = new();
        private HashSet<string> _statusFilters = new();
        private string _searchText = string.Empty;
        private string _currentClassId = "";

        // ==================== CONSTRUCTOR ====================

        public ExamManagementView()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager();
        }

        public ExamManagementView(DatabaseManager dbManager = null)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        // ==================== LIFECYCLE ====================
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeFilters();

            if (_dbManager != null)
            {
                await LoadDataFromFirebaseAsync();
            }
            else
            {
                LoadSampleData();
            }
        }

        private void InitializeFilters()
        {
            _statusFilters.Clear();

            // All unchecked by default
            ChkActive.IsChecked = false;
            ChkClosed.IsChecked = false;
            ChkDraft.IsChecked = false;
        }

        // ==================== DATA LOADING ====================
        private void LoadSampleData()
        {
            // TODO: Load từ Firebase sau khi integrate
            _allExams = new List<Exam>
            {
                new()
                {
                    Id = "exam_1",
                    ClassId = "class_1",
                    ClassName = "SE104.O21 - Công nghệ phần mềm",
                    Title = "Quiz Chương 1: Giới Thiệu Lập Trình",
                    Description = "Kiểm tra kiến thức cơ bản về C# và OOP",
                    TotalQuestions = 10,
                    TimeLimitMinutes = 30,
                    PassingScore = 50,
                    IsActive = true,
                    IsPublished = true,
                    CreatedAt = DateTime.Now.AddDays(-5),
                    UpdatedAt = DateTime.Now.AddDays(-5)
                },
                new()
                {
                    Id = "exam_2",
                    ClassId = "class_1",
                    ClassName = "SE104.O21 - Công nghệ phần mềm",
                    Title = "Bài Tập: Xây Dựng Ứng Dụng WPF",
                    Description = "Tạo ứng dụng WPF đơn giản với MVVM pattern",
                    TotalQuestions = 1,
                    TimeLimitMinutes = 120,
                    PassingScore = 60,
                    IsActive = true,
                    IsPublished = true,
                    CreatedAt = DateTime.Now.AddDays(-3),
                    UpdatedAt = DateTime.Now.AddDays(-3)
                },
                new()
                {
                    Id = "exam_3",
                    ClassId = "class_1",
                    ClassName = "SE104.O21 - Công nghệ phần mềm",
                    Title = "Giữa Kỳ: Database & ORM",
                    Description = "Bài thi giữa kỳ về cơ sở dữ liệu và Entity Framework",
                    TotalQuestions = 25,
                    TimeLimitMinutes = 90,
                    PassingScore = 55,
                    IsActive = false,
                    IsPublished = true,
                    ScheduledDate = DateTime.Now.AddDays(7),
                    CreatedAt = DateTime.Now.AddDays(-10),
                    UpdatedAt = DateTime.Now.AddDays(-2)
                },
                new()
                {
                    Id = "exam_4",
                    ClassId = "class_1",
                    ClassName = "CS101 - Nhập môn lập trình",
                    Title = "Luyện Tập: API Rest",
                    Description = "Luyện tập xây dựng REST API với ASP.NET Core",
                    TotalQuestions = 5,
                    TimeLimitMinutes = 60,
                    PassingScore = 50,
                    IsActive = true,
                    IsPublished = false,
                    CreatedAt = DateTime.Now.AddDays(-1),
                    UpdatedAt = DateTime.Now.AddHours(-2)
                },
                new()
                {
                    Id = "exam_5",
                    ClassId = "class_1",
                    ClassName = "CS101 - Nhập môn lập trình",
                    Title = "Cuối Kỳ: Toàn Bộ Môn Học",
                    Description = "Bài thi cuối kỳ bao gồm toàn bộ nội dung đã học",
                    TotalQuestions = 50,
                    TimeLimitMinutes = 120,
                    PassingScore = 60,
                    IsActive = false,
                    IsPublished = true,
                    ScheduledDate = DateTime.Now.AddDays(30),
                    CreatedAt = DateTime.Now.AddDays(-20),
                    UpdatedAt = DateTime.Now.AddDays(-5)
                }
            };

            Refresh();
        }

        /// <summary>
        /// Load dữ liệu từ Firebase
        /// </summary>
        private async Task LoadDataFromFirebaseAsync()
        {
            try
            {
                if (_dbManager == null)
                {
                    CustomDialog.Show("Lỗi: DatabaseManager chưa được khởi tạo!", "Lỗi", DialogType.Error);
                    return;
                }

                // Lấy bài thi từ Firebase
                if (!string.IsNullOrEmpty(_currentClassId))
                {
                    _allExams = await _dbManager.GetExamsByClassAsync(_currentClassId);
                }
                else
                {
                    _allExams = await _dbManager.GetAllExamsForInstructorAsync();
                }

                if (_allExams == null) _allExams = new List<Exam>();

                // Update combo box subjects if available
                if (CboSubject != null)
                {
                    CboSubject.ItemsSource = null;
                    CboSubject.Items.Clear();

                    var subjects = _allExams.Select(e => e.ClassName).Distinct().ToList();
                    subjects.Insert(0, "Tất cả môn học");
                    CboSubject.ItemsSource = subjects;
                    CboSubject.SelectedIndex = 0;
                }

                Refresh();
            }
            catch (Exception ex)
            {
                CustomDialog.Show($"❌ Lỗi tải dữ liệu:\n{ex.Message}", "Lỗi", DialogType.Error);
            }
        }

        // ==================== FILTERING & RENDERING ====================
        private IEnumerable<Exam> GetFilteredExams()
        {
            var filtered = _allExams.AsEnumerable();

            // Status filter
            if (_statusFilters.Count > 0)
            {
                filtered = filtered.Where(e =>
                {
                    if (_statusFilters.Contains("active") && e.IsActive) return true;
                    if (_statusFilters.Contains("closed") && !e.IsActive && e.IsPublished) return true;
                    if (_statusFilters.Contains("draft") && !e.IsPublished) return true;
                    return false;
                });
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(_searchText))
                filtered = filtered.Where(e =>
                    e.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            // Subject filter
            if (CboSubject != null && CboSubject.SelectedItem != null)
            {
                string subject = "";
                if (CboSubject.SelectedItem is ComboBoxItem cbi) subject = cbi.Content?.ToString();
                else subject = CboSubject.SelectedItem.ToString();

                if (subject != "Tất cả môn học" && !string.IsNullOrEmpty(subject))
                {
                    filtered = filtered.Where(e => e.ClassName == subject);
                }
            }

            return filtered;
        }

        private void Refresh()
        {
            if (ExamsList == null) return;

            var filtered = GetFilteredExams().ToList();

            // Update statistics
            UpdateStats();

            // Render exams using ListBox binding
            ExamsList.ItemsSource = filtered;
            
            if (filtered.Count == 0)
            {
                if (EmptyState != null)
                    EmptyState.Visibility = Visibility.Visible;
                ExamsList.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (EmptyState != null)
                    EmptyState.Visibility = Visibility.Collapsed;
                ExamsList.Visibility = Visibility.Visible;
            }
        }

        private void UpdateStats()
        {
            if (TxtTotalExams == null || TxtActiveExams == null || TxtPendingExams == null) return;

            TxtTotalExams.Text = _allExams.Count.ToString();
            TxtActiveExams.Text = _allExams.Count(e => e.IsActive).ToString();
            TxtPendingExams.Text = _allExams.Count(e => !e.IsPublished).ToString();
        }

        

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Tạo TextBlock label cho dialog
        /// </summary>
        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        /// <summary>
        /// Tạo TextBox input
        /// </summary>
        private static TextBox CreateTextBox(string text = "")
        {
            return new TextBox
            {
                Text = text,
                Height = 40,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// Tạo ComboBox
        /// </summary>
        private static ComboBox CreateComboBox(string[] items)
        {
            return new ComboBox
            {
                ItemsSource = items,
                Height = 40,
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1)
            };
        }

        // ==================== EVENT HANDLERS ====================

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb == null) return;

             if (cb.Name == "ChkActive")
            {
                if (cb.IsChecked == true) _statusFilters.Add("active");
                else _statusFilters.Remove("active");
            }
            else if (cb.Name == "ChkClosed")
            {
                if (cb.IsChecked == true) _statusFilters.Add("closed");
                else _statusFilters.Remove("closed");
            }
            else if (cb.Name == "ChkDraft")
            {
                if (cb.IsChecked == true) _statusFilters.Add("draft");
                else _statusFilters.Remove("draft");
            }

            Refresh();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = TxtSearch.Text;
            Refresh();
        }

        private void ExamCard_ViewClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Exam exam)
            {
                ViewExam(exam);
            }
        }

        private void ExamCard_EditClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Exam exam)
            {
                EditExam(exam);
            }
        }

        private void ExamCard_DeleteClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Exam exam)
            {
                DeleteExam(exam);
            }
        }

        private void BtnCreateExam_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new CreateExamView(_dbManager));
        }

        private void ViewExam(Exam exam)
        {
            if (Window.GetWindow(this) is MainWindow mw)
            {
                mw.NavigateTo(new e_learning_app.ExamReportView(exam, _dbManager));
            }
        }

        private void EditExam(Exam exam)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new EditExamView(_dbManager, exam));
        }

        private async void DeleteExam(Exam exam)
        {
            var confirmed = CustomDialog.Confirm(
                $"Bạn có chắc chắn muốn xóa bài thi:\n\n\"{exam.Title}\"?\n\nHành động này không thể hoàn tác.",
                "Xác Nhận Xóa", "Xóa ngay", "Hủy", DialogType.Warning);

            if (confirmed)
            {
                try
                {
                    // Xóa từ Firebase
                    bool success = false;
                    if (_dbManager != null)
                    {
                        success = await _dbManager.DeleteExamAsync(exam.Id);
                    }

                    if (success || _dbManager == null)
                    {
                        _allExams.Remove(exam);
                        Refresh();
                        CustomDialog.Show("✅ Xóa bài thi thành công!", "Thành Công", DialogType.Success);
                    }
                    else
                    {
                        throw new Exception("Firebase từ chối thao tác xóa.");
                    }
                }
                catch (Exception ex)
                {
                    CustomDialog.Show($"❌ Lỗi khi xóa:\n{ex.Message}", "Lỗi", DialogType.Error);
                }
            }
        }              
    }
}