using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace e_learning_app.Views
{
    public partial class CourseDetailView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Course _course;
        private readonly string _userRole;
        private string _selectedVideoPath = string.Empty;

        public CourseDetailView(DatabaseManager dbManager, Course course, string role = "Teacher")
        {
            InitializeComponent();
            _dbManager = dbManager;
            _course = course;
            _userRole = role;

            if (_userRole == "Student")
            {
                BtnMoreActions.Visibility = Visibility.Collapsed;
                BtnAddLesson.Visibility = Visibility.Collapsed;
            }

            InitializeYearComboBox();
            UpdateUI();
            LoadLessonsAsync();
        }

        private void InitializeYearComboBox()
        {
            int currentYear = DateTime.Now.Year;
            EditYearInput.Items.Clear();
            for (int i = currentYear - 2; i <= currentYear + 3; i++)
            {
                EditYearInput.Items.Add($"{i}-{i + 1}");
            }
        }

        private void UpdateUI()
        {
            if (_course == null) return;

            TxtTitle.Text = _course.Title;
            TxtEmoji.Text = _course.Emoji;
            TxtClassInfo.Text = $"{_course.ClassName}  •  {_course.Semester}";
            TxtCategory.Text = string.IsNullOrWhiteSpace(_course.Category) ? "Chung" : _course.Category;
            TxtCourseType.Text = string.IsNullOrWhiteSpace(_course.CourseType) ? "Đại cương" : _course.CourseType;
            TxtDescription.Text = string.IsNullOrWhiteSpace(_course.Description) ? "Chưa có mô tả chi tiết." : _course.Description;
            TxtStudentCount.Text = _course.StudentCount.ToString();

            var converter = new BrushConverter();
            try
            {
                CoverPhoto.Background = (SolidColorBrush)converter.ConvertFromString(_course.AccentColor) ?? Brushes.SlateBlue;
            }
            catch
            {
                CoverPhoto.Background = Brushes.SlateBlue;
            }

            if (_course.IsActive)
            {
                MenuToggleStatus.Header = "Kết thúc lớp học";
                MenuToggleIcon.Text = "⏸️";
                MenuToggleStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            }
            else
            {
                MenuToggleStatus.Header = "Kích hoạt lại lớp";
                MenuToggleIcon.Text = "▶️";
                MenuToggleStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            EditTitleInput.Text = _course.Title;
            EditDescInput.Text = _course.Description;
            EditClassInput.Text = _course.ClassName;
            EditEmojiInput.Text = _course.Emoji;

            // Added Category Binding
            EditCategoryInput.Text = _course.Category;

            // Set the correct RadioButton color based on the saved course color
            SetSelectedColor(_course.AccentColor);

            SetComboBoxByContent(EditTypeInput, _course.CourseType);

            if (!string.IsNullOrEmpty(_course.Semester) && _course.Semester.Contains(" - "))
            {
                string[] parts = _course.Semester.Split(new[] { " - " }, StringSplitOptions.None);
                SetComboBoxByContent(EditSemesterInput, parts[0]);
                EditYearInput.SelectedItem = parts.Length > 1 ? parts[1] : null;
            }

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            EditDrawer.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(_course.Semester) && _course.Semester.Contains(" - "))
            {
                // Use a more robust split to handle potential spacing issues
                string[] parts = _course.Semester.Split(new[] { " - " }, StringSplitOptions.None);

                // Set Semester (Hoc ky 1, 2, etc.)
                SetComboBoxByContent(EditSemesterInput, parts[0].Trim());

                // Set Year (2024-2025)
                if (parts.Length > 1)
                {
                    string yearValue = parts[1].Trim();

                    // Loop through items to find the match manually to avoid reference issues
                    foreach (var item in EditYearInput.Items)
                    {
                        if (item.ToString() == yearValue)
                        {
                            EditYearInput.SelectedItem = item;
                            break;
                        }
                    }
                }
            }

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            EditDrawer.Visibility = Visibility.Visible;
        }

        private void SetComboBoxByContent(ComboBox cb, string value)
        {
            foreach (ComboBoxItem item in cb.Items)
            {
                if (item.Content.ToString() == value)
                {
                    cb.SelectedItem = item;
                    return;
                }
            }
        }

        private async void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        {
            _course.Title = EditTitleInput.Text;
            _course.Description = EditDescInput.Text;
            _course.ClassName = EditClassInput.Text;
            _course.Emoji = EditEmojiInput.Text;

            // Save Category
            _course.Category = EditCategoryInput.Text;

            // Save Color from RadioButtons
            _course.AccentColor = GetSelectedColor();

            _course.CourseType = (EditTypeInput.SelectedItem as ComboBoxItem)?.Content.ToString();

            string sem = (EditSemesterInput.SelectedItem as ComboBoxItem)?.Content.ToString();
            string year = EditYearInput.SelectedItem?.ToString();
            _course.Semester = $"{sem} - {year}";

            if (await _dbManager.UpdateCourseAsync(_course))
            {
                UpdateUI();
                CloseEditDrawer_Click(null, null);
            }
        }

        private void CloseEditDrawer_Click(object sender, RoutedEventArgs e)
        {
            EditDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            DeleteOverlay.Visibility = Visibility.Visible;
        }

        private async void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (await _dbManager.DeleteCourseAsync(_course.Id)) NavigateBack();
        }

        private void CloseDeleteModal_Click(object sender, RoutedEventArgs e)
        {
            DeleteOverlay.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private async void MenuToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            _course.IsActive = !_course.IsActive;
            if (await _dbManager.UpdateCourseAsync(_course)) UpdateUI();
        }

        private void BtnMoreActions_Click(object sender, RoutedEventArgs e)
        {
            BtnMoreActions.ContextMenu.PlacementTarget = BtnMoreActions;
            BtnMoreActions.ContextMenu.IsOpen = true;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            if (_userRole == "Teacher")
            {
                var mainWin = Window.GetWindow(this) as MainWindow;
                if (mainWin != null) mainWin.MainContentArea.Content = new MyClassesView(_dbManager, _userRole);
            }
            else if (_userRole == "Student")
            {
                var studentWin = Window.GetWindow(this) as StudentMainWindow;
                if (studentWin != null) studentWin.StudentContentArea.Content = new MyClassesView(_dbManager, _userRole);
            }
        }

        private async void LoadLessonsAsync()
        {
            DocumentsList.Items.Clear();

            List<Lesson> lessons = await _dbManager.GetLessonsByCourseAsync(_course.Id);

            foreach (var lesson in lessons)
            {
                var border = new Border { Style = (Style)FindResource("DocumentBorderStyle"), Cursor = System.Windows.Input.Cursors.Hand };
                border.MouseLeftButtonDown += (s, e) => {
                    if (_userRole == "Student")
                    {
                        var studentWin = Window.GetWindow(this) as StudentMainWindow;
                        if (studentWin != null)
                        {
                            studentWin.StudentContentArea.Content = new StudentCourseView(_dbManager, _course, lesson);
                        }
                    }
                    else if (_userRole == "Teacher")
                    {
                        // Giảng viên cũng có thể click vào xem video bài giảng (nếu muốn hỗ trợ)
                    }
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var iconBorder = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF")), CornerRadius = new CornerRadius(8), Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Left };
                var icon = new TextBlock { Text = "▶️", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                iconBorder.Child = icon;
                Grid.SetColumn(iconBorder, 0);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                textStack.Children.Add(new TextBlock { Text = lesson.Title, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")) });
                textStack.Children.Add(new TextBlock { Text = "Video Bài Giảng", FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")), Margin = new Thickness(0, 2, 0, 0) });
                Grid.SetColumn(textStack, 1);

                var enterBtn = new TextBlock { Text = "Vào học →", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(enterBtn, 2);

                grid.Children.Add(iconBorder);
                grid.Children.Add(textStack);
                if (_userRole == "Student")
                {
                    grid.Children.Add(enterBtn);
                }
                else if (_userRole == "Teacher")
                {
                    var deleteBtn = new TextBlock { Text = "🗑️", FontSize = 14, Foreground = Brushes.Red, VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(10, 0, 0, 0) };
                    deleteBtn.MouseLeftButtonDown += async (s, e) =>
                    {
                        e.Handled = true; // Prevent triggering the border click
                        var result = MessageBox.Show($"Bạn có chắc chắn muốn xóa bài học '{lesson.Title}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            if (await _dbManager.DeleteLessonAsync(lesson.Id))
                            {
                                LoadLessonsAsync();
                            }
                        }
                    };
                    Grid.SetColumn(deleteBtn, 2);
                    grid.Children.Add(deleteBtn);
                }

                border.Child = grid;
                DocumentsList.Items.Add(border);
            }
        }

        // --- ADD LESSON LOGIC ---
        private void BtnAddLesson_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            AddLessonOverlay.Visibility = Visibility.Visible;
            InputLessonTitle.Text = string.Empty;
            InputLessonDesc.Text = string.Empty;
            InputVideoPath.Text = "Chưa chọn file...";
            _selectedVideoPath = string.Empty;
            UploadProgressPanel.Visibility = Visibility.Collapsed;
        }

        private void CloseAddLessonOverlay_Click(object sender, RoutedEventArgs e)
        {
            AddLessonOverlay.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private void BtnSelectVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov|All Files|*.*";
            openFileDialog.Title = "Chọn Video Bài Giảng";

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedVideoPath = openFileDialog.FileName;
                InputVideoPath.Text = System.IO.Path.GetFileName(_selectedVideoPath);
            }
        }

        private async void BtnSaveLesson_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputLessonTitle.Text))
            {
                MessageBox.Show("Vui lòng nhập tên bài học.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_selectedVideoPath))
            {
                MessageBox.Show("Vui lòng chọn một video bài giảng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable buttons to prevent multiple clicks
            BtnSaveLesson.IsEnabled = false;
            UploadProgressPanel.Visibility = Visibility.Visible;

            try
            {
                CloudinaryService cloudinary = new CloudinaryService();
                string videoUrl = await cloudinary.UploadVideoAsync(_selectedVideoPath, "e_learning_videos");

                Lesson newLesson = new Lesson
                {
                    CourseId = _course.Id,
                    Title = InputLessonTitle.Text,
                    Description = InputLessonDesc.Text,
                    VideoUrl = videoUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbManager.AddLessonAsync(newLesson);

                MessageBox.Show("Đăng bài học và video thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                CloseAddLessonOverlay_Click(null, null);
                LoadLessonsAsync(); // Reload the list
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải video lên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSaveLesson.IsEnabled = true;
                UploadProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        // --- HELPER METHODS FOR COLOR RADIO BUTTONS ---

        private void SetSelectedColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return;
            try
            {
                Color targetColor = (Color)ColorConverter.ConvertFromString(hexColor);
                var radioButtons = FindVisualChildren<RadioButton>(EditDrawer).Where(r => r.GroupName == "ThemeColors");
                foreach (var rb in radioButtons)
                {
                    if (rb.Background is SolidColorBrush brush && brush.Color == targetColor)
                    {
                        rb.IsChecked = true;
                        break;
                    }
                }
            }
            catch { /* Ignore invalid hex strings */ }
        }

        private string GetSelectedColor()
        {
            var radioButtons = FindVisualChildren<RadioButton>(EditDrawer).Where(r => r.GroupName == "ThemeColors");
            foreach (var rb in radioButtons)
            {
                if (rb.IsChecked == true && rb.Background is SolidColorBrush brush)
                {
                    // Convert the brush color back to a HEX string (format #RRGGBB)
                    return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                }
            }
            return "#3B82F6"; // Default Blue if nothing is checked
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}