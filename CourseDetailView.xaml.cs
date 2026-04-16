using e_learning_app;
using e_learning_app.Class;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace e_learning_app.Views
{
    public partial class CourseDetailView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Course _course;
        private string CurrentUserId => _dbManager.GetCurrentUser()?.Id;
        private ObservableCollection<CourseContent> _courseContents;

        // Variables for editing/deleting content
        private CourseContent _editingContent = null;
        private CourseContent _contentToDelete = null;

        // Change this to your actual network IP path!
        private const string LAN_SHARED_FOLDER = @"\\192.168.100.130\ELearningShared";

        public CourseDetailView(DatabaseManager dbManager, Course course)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _course = course;

            InitializeYearComboBox();
            ApplyRolePermissions();
            LoadCourseContent();
            UpdateUI();
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

        private void ApplyRolePermissions()
        {
            bool isInstructor = _course.InstructorId == CurrentUserId;

            BtnMoreActions.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnAddContent.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LoadCourseContent()
        {
            try
            {
                var contents = await _dbManager.GetCourseContentsAsync(_course.Id);

                if (contents != null)
                {
                    _courseContents = new ObservableCollection<CourseContent>(contents.OrderBy(c => c.OrderIndex));
                    DocumentsList.ItemsSource = _courseContents;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải nội dung: " + ex.Message);
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

        // ==========================================
        // File, Link, and Note Interaction Logic
        // ==========================================
        private void DocumentsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DocumentsList.SelectedItem is CourseContent selectedContent)
            {
                try
                {
                    if (selectedContent.Type == "Link")
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = selectedContent.Data,
                            UseShellExecute = true
                        });
                    }
                    else if (selectedContent.Type == "Document")
                    {
                        if (!File.Exists(selectedContent.Data))
                        {
                            MessageBox.Show("Không thể tìm thấy tệp. Máy chủ chứa tệp có thể đang tắt hoặc bạn không cùng chung mạng Wi-Fi.", "Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = selectedContent.Data,
                            UseShellExecute = true
                        });
                    }
                    else if (selectedContent.Type == "Note")
                    {
                        TxtNoteTitle.Text = selectedContent.Title;
                        TxtNoteContent.Text = selectedContent.Data;

                        MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                        NoteReaderDrawer.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở tệp hoặc liên kết: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseNoteReader_Click(object sender, RoutedEventArgs e)
        {
            NoteReaderDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_course.InstructorId != CurrentUserId) return;

            var dragHandle = sender as FrameworkElement;
            var item = dragHandle?.DataContext as CourseContent;

            if (item != null)
            {
                DragDrop.DoDragDrop(dragHandle, item, DragDropEffects.Move);
            }
        }

        private async void DocumentsList_Drop(object sender, DragEventArgs e)
        {
            if (_course.InstructorId != CurrentUserId) return;

            var droppedData = e.Data.GetData(typeof(CourseContent)) as CourseContent;
            var targetElement = e.OriginalSource as FrameworkElement;
            var target = targetElement?.DataContext as CourseContent;

            if (droppedData != null && target != null && droppedData != target)
            {
                int oldIndex = _courseContents.IndexOf(droppedData);
                int newIndex = _courseContents.IndexOf(target);

                _courseContents.RemoveAt(oldIndex);
                _courseContents.Insert(newIndex, droppedData);

                for (int i = 0; i < _courseContents.Count; i++)
                {
                    _courseContents[i].OrderIndex = i;
                }

                try
                {
                    await _dbManager.UpdateCourseContentOrderAsync(_course.Id, _courseContents.ToList());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi lưu thứ tự mới: " + ex.Message);
                    LoadCourseContent();
                }
            }
        }

        // ==========================================
        // Content Editing & Deleting Logic
        // ==========================================
        private void BtnEditContent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string contentId)
            {
                _editingContent = _courseContents.FirstOrDefault(c => c.Id == contentId);
                if (_editingContent == null) return;

                TxtContentFormTitle.Text = "Chỉnh sửa nội dung";
                BtnSubmitContent.Content = "Lưu thay đổi";

                AddTitleInput.Text = _editingContent.Title;
                SetComboBoxByContent(AddTypeInput, _editingContent.Type);

                AddLinkInput.Text = "";
                AddNoteInput.Text = "";
                AddDocPathInput.Text = "";

                if (_editingContent.Type == "Link") AddLinkInput.Text = _editingContent.Data;
                else if (_editingContent.Type == "Note") AddNoteInput.Text = _editingContent.Data;
                else if (_editingContent.Type == "Document") AddDocPathInput.Text = _editingContent.Data;

                MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                AddContentDrawer.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteContent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string contentId)
            {
                _contentToDelete = _courseContents.FirstOrDefault(c => c.Id == contentId);
                if (_contentToDelete != null)
                {
                    MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                    DeleteContentOverlay.Visibility = Visibility.Visible;
                }
            }
        }

        private void CloseDeleteContentModal_Click(object sender, RoutedEventArgs e)
        {
            DeleteContentOverlay.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
            _contentToDelete = null;
        }

        private async void ConfirmDeleteContent_Click(object sender, RoutedEventArgs e)
        {
            if (_contentToDelete != null)
            {
                if (await _dbManager.DeleteCourseContentAsync(_course.Id, _contentToDelete.Id))
                {
                    // Delete the physical file from the LAN folder
                    if (_contentToDelete.Type == "Document")
                    {
                        try
                        {
                            if (File.Exists(_contentToDelete.Data))
                            {
                                File.Delete(_contentToDelete.Data);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Không thể xóa tệp vật lý: " + ex.Message);
                        }
                    }

                    _courseContents.Remove(_contentToDelete);
                }
            }
            CloseDeleteContentModal_Click(null, null);
        }

        // ==========================================
        // Add/Edit Content Drawer Logic
        // ==========================================
        private void BtnAddContent_Click(object sender, RoutedEventArgs e)
        {
            _editingContent = null;

            TxtContentFormTitle.Text = "Thêm nội dung mới";
            BtnSubmitContent.Content = "Thêm vào lớp";

            AddTitleInput.Text = "";
            AddLinkInput.Text = "";
            AddNoteInput.Text = "";
            AddDocPathInput.Text = "";
            AddTypeInput.SelectedIndex = 0;

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            AddContentDrawer.Visibility = Visibility.Visible;
        }

        private void CloseAddDrawer_Click(object sender, RoutedEventArgs e)
        {
            AddContentDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private void AddTypeInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputAreaDocument != null) InputAreaDocument.Visibility = Visibility.Collapsed;
            if (InputAreaLink != null) InputAreaLink.Visibility = Visibility.Collapsed;
            if (InputAreaNote != null) InputAreaNote.Visibility = Visibility.Collapsed;

            var selectedItem = AddTypeInput.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string type = selectedItem.Content.ToString();
                if (type == "Document" && InputAreaDocument != null) InputAreaDocument.Visibility = Visibility.Visible;
                else if (type == "Link" && InputAreaLink != null) InputAreaLink.Visibility = Visibility.Visible;
                else if (type == "Note" && InputAreaNote != null) InputAreaNote.Visibility = Visibility.Visible;
            }
        }

        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files (*.*)|*.*|PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|PowerPoint (*.pptx)|*.pptx";

            if (openFileDialog.ShowDialog() == true)
            {
                AddDocPathInput.Text = openFileDialog.FileName;

                if (string.IsNullOrWhiteSpace(AddTitleInput.Text))
                {
                    AddTitleInput.Text = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                }
            }
        }

        private async void ConfirmAddContent_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AddTitleInput.Text))
            {
                MessageBox.Show("Vui lòng nhập tiêu đề!");
                return;
            }

            string type = (AddTypeInput.SelectedItem as ComboBoxItem)?.Content.ToString();
            string data = "";

            if (type == "Link")
            {
                data = AddLinkInput.Text;
                if (string.IsNullOrWhiteSpace(data)) { MessageBox.Show("Vui lòng nhập link!"); return; }
            }
            else if (type == "Note")
            {
                data = AddNoteInput.Text;
                if (string.IsNullOrWhiteSpace(data)) { MessageBox.Show("Vui lòng nhập ghi chú!"); return; }
            }
            else if (type == "Document")
            {
                string sourcePath = AddDocPathInput.Text;
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    MessageBox.Show("Vui lòng chọn tệp hợp lệ!");
                    return;
                }

                // If it's a NEW file from the PC (not an existing network path)
                if (!sourcePath.StartsWith(@"\\") && File.Exists(sourcePath))
                {
                    try
                    {
                        if (!Directory.Exists(LAN_SHARED_FOLDER))
                        {
                            MessageBox.Show($"Không thể kết nối đến thư mục mạng: {LAN_SHARED_FOLDER}.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        string fileName = Path.GetFileName(sourcePath);
                        string uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                        string destinationPath = Path.Combine(LAN_SHARED_FOLDER, uniqueFileName);

                        File.Copy(sourcePath, destinationPath);
                        data = destinationPath;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi lưu tệp vào máy chủ mạng: " + ex.Message);
                        return;
                    }
                }
                else
                {
                    data = sourcePath; // Use existing path
                }
            }

            if (_editingContent != null)
            {
                // Delete the old file if it was replaced/changed
                if (_editingContent.Type == "Document" && _editingContent.Data != data)
                {
                    try
                    {
                        if (File.Exists(_editingContent.Data))
                        {
                            File.Delete(_editingContent.Data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Không thể xóa tệp cũ: " + ex.Message);
                    }
                }

                // -- EDIT EXISTING CONTENT --
                _editingContent.Title = AddTitleInput.Text;
                _editingContent.Type = type;
                _editingContent.Data = data;

                if (await _dbManager.UpdateCourseContentAsync(_course.Id, _editingContent))
                {
                    int index = _courseContents.IndexOf(_editingContent);
                    _courseContents.RemoveAt(index);
                    _courseContents.Insert(index, _editingContent);
                    DocumentsList.SelectedItem = null;
                }
            }
            else
            {
                // -- ADD NEW CONTENT --
                int nextOrderIndex = _courseContents != null && _courseContents.Any() ? _courseContents.Max(c => c.OrderIndex) + 1 : 0;

                var newContent = new CourseContent
                {
                    CourseId = _course.Id,
                    Title = AddTitleInput.Text,
                    Type = type,
                    Data = data,
                    OrderIndex = nextOrderIndex
                };

                var collectionRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id).Collection("Contents");
                var docRef = await collectionRef.AddAsync(newContent);

                newContent.Id = docRef.Id;
                if (_courseContents == null) _courseContents = new ObservableCollection<CourseContent>();
                _courseContents.Add(newContent);
            }

            CloseAddDrawer_Click(null, null);
        }

        // ==========================================
        // Course Editing Logic (Class level stuff)
        // ==========================================
        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            EditTitleInput.Text = _course.Title;
            EditDescInput.Text = _course.Description;
            EditClassInput.Text = _course.ClassName;
            EditEmojiInput.Text = _course.Emoji;
            EditCategoryInput.Text = _course.Category;

            SetSelectedColor(_course.AccentColor);
            SetComboBoxByContent(EditTypeInput, _course.CourseType);

            if (!string.IsNullOrEmpty(_course.Semester) && _course.Semester.Contains(" - "))
            {
                string[] parts = _course.Semester.Split(new[] { " - " }, StringSplitOptions.None);
                SetComboBoxByContent(EditSemesterInput, parts[0].Trim());

                if (parts.Length > 1)
                {
                    string yearValue = parts[1].Trim();
                    foreach (var item in EditYearInput.Items)
                    {
                        if (item.ToString() == yearValue) { EditYearInput.SelectedItem = item; break; }
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
                if (item.Content.ToString() == value) { cb.SelectedItem = item; return; }
            }
        }

        private async void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        {
            _course.Title = EditTitleInput.Text;
            _course.Description = EditDescInput.Text;
            _course.ClassName = EditClassInput.Text;
            _course.Emoji = EditEmojiInput.Text;
            _course.Category = EditCategoryInput.Text;
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
            var mainWin = Window.GetWindow(this) as MainWindow;
            if (mainWin != null) mainWin.MainContentArea.Content = new MyClassesView(_dbManager, CurrentUserId);
        }

        // ==========================================
        // Helper Methods
        // ==========================================
        private void SetSelectedColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return;
            try
            {
                Color targetColor = (Color)ColorConverter.ConvertFromString(hexColor);
                var radioButtons = FindVisualChildren<RadioButton>(EditDrawer).Where(r => r.GroupName == "ThemeColors");
                foreach (var rb in radioButtons)
                {
                    if (rb.Background is SolidColorBrush brush && brush.Color == targetColor) { rb.IsChecked = true; break; }
                }
            }
            catch { }
        }

        private string GetSelectedColor()
        {
            var radioButtons = FindVisualChildren<RadioButton>(EditDrawer).Where(r => r.GroupName == "ThemeColors");
            foreach (var rb in radioButtons)
            {
                if (rb.IsChecked == true && rb.Background is SolidColorBrush brush)
                {
                    return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                }
            }
            return "#3B82F6";
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T) yield return (T)child;
                    foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }

        // ==========================================
        // Click-to-Unfocus Logic
        // ==========================================
        private void UserControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickedElement = e.OriginalSource as DependencyObject;
            var clickedItem = FindVisualParent<ListBoxItem>(clickedElement);

            if (clickedItem == null)
            {
                DocumentsList.SelectedItem = null;
            }
        }

        public static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = child;
            while (parentObject != null)
            {
                if (parentObject is T parent) return parent;

                if (parentObject is FrameworkContentElement contentElement)
                {
                    parentObject = contentElement.Parent;
                }
                else
                {
                    parentObject = VisualTreeHelper.GetParent(parentObject);
                }
            }
            return null;
        }
    }
}