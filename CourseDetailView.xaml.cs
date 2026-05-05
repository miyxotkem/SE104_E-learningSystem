using e_learning_app.Class;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace e_learning_app.Views
{
    public class PendingRequest
    {
        public string RegistrationId { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime RequestedAt { get; set; }

        public string TimeAgo
        {
            get
            {
                TimeSpan span = DateTime.Now - RequestedAt;
                if (span.Days > 365) return $"{span.Days / 365} năm trước";
                if (span.Days > 30) return $"{span.Days / 30} tháng trước";
                if (span.Days > 0) return $"{span.Days} ngày trước";
                if (span.Hours > 0) return $"{span.Hours} giờ trước";
                if (span.Minutes > 0) return $"{span.Minutes} phút trước";
                return "Vừa xong";
            }
        }
    }

    public class EnrolledStudent
    {
        public string RegistrationId { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime ApprovedDate { get; set; }
    }

    public class GradingItem
    {
        public string SubmissionId { get; set; }
        public string StudentId { get; set; }
        public string FullName { get; set; }
        public string FileUrl { get; set; }
        public string DisplayFileName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public bool IsLate { get; set; }
        public double? Score { get; set; }
        public string Comment { get; set; }
        public string StatusColor => Score.HasValue ? "#10B981" : "#F59E0B";
    }

    public class DeadlineColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime deadline)
            {
                if (deadline < DateTime.Now)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                else
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class CourseDetailView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private readonly Course _course;
        private string CurrentUserId => _dbManager.GetCurrentUser()?.Id;
        private ObservableCollection<CourseContent> _courseContents;
        private ObservableCollection<Assignment> _assignments;

        private CourseContent _editingContent = null;
        private CourseContent _contentToDelete = null;

        private Assignment _currentViewedAssignment = null;
        private string _selectedSubmissionFilePath = "";

        private Assignment _editingAssignment = null;
        private Assignment _assignmentToDelete = null;

        private string _selectedAssignFilePath = "";
        private readonly Cloudinary _cloudinary;

        private ObservableCollection<GradingItem> _gradingList = new();
        private GradingItem _currentGradingItem = null;

        public Brush CourseAccentBrush
        {
            get { return (Brush)GetValue(CourseAccentBrushProperty); }
            set { SetValue(CourseAccentBrushProperty, value); }
        }
        public static readonly DependencyProperty CourseAccentBrushProperty =
            DependencyProperty.Register("CourseAccentBrush", typeof(Brush), typeof(CourseDetailView), new PropertyMetadata(Brushes.SlateBlue));

        public CourseDetailView(DatabaseManager dbManager, Course course)
        {
            InitializeComponent();
            _dbManager = dbManager;
            _course = course;

            Account account = new Account(
                "drg8swbxp",
                "167723827683986",
                "3aclNKhg3htYds76wcUrxjTdnRU");

            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;

            InitializeYearComboBox();
            ApplyRolePermissions();
            LoadCourseContent();
            LoadAssignments();
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

        private async void ApplyRolePermissions()
        {
            bool isInstructor = _course.InstructorId == CurrentUserId;
            BtnMoreActions.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnAddContent.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnShowAddAssignment.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;

            BtnManageApprovals.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnManageStudents.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;

            if (isInstructor)
            {
                await UpdatePendingBadgeAsync();
            }
        }

        private async Task UpdatePendingBadgeAsync()
        {
            try
            {
                var snap = await _dbManager.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", _course.Id)
                    .WhereEqualTo("status", "pending")
                    .GetSnapshotAsync();

                int count = snap.Count;
                if (count > 0)
                {
                    BadgeBorder.Visibility = Visibility.Visible;
                    TxtPendingCount.Text = count > 99 ? "99+" : count.ToString();
                }
                else
                {
                    BadgeBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
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
            TxtSchedule.Text = _course.DayOfWeek == "Hình thức 2"
                               ? "Hình thức 2"
                               : $"{_course.DayOfWeek} (Tiết {_course.StartPeriod}-{_course.EndPeriod})";
            TxtDescription.Text = string.IsNullOrWhiteSpace(_course.Description) ? "Chưa có mô tả chi tiết." : _course.Description;

            TxtStudentCount.Text = _course.StudentCount.ToString();
            TxtAssignmentCount.Text = _course.AssignmentCount.ToString();

            var converter = new BrushConverter();
            Brush accentBrush = Brushes.SlateBlue;
            try { accentBrush = (SolidColorBrush)converter.ConvertFromString(_course.AccentColor); }
            catch { }

            CoverPhoto.Background = accentBrush;
            CourseAccentBrush = accentBrush;

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
            LoadInstructorName();
        }

        private async void LoadInstructorName()
        {
            try
            {
                var doc = await _dbManager.GetDb.Collection("Users").Document(_course.InstructorId).GetSnapshotAsync();
                TxtInstructorName.Text = doc.Exists ? doc.GetValue<string>("FullName") : "Không xác định";
            }
            catch
            {
                TxtInstructorName.Text = "Lỗi tải tên";
            }
        }

        private void DocumentsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DocumentsList.SelectedItem is CourseContent selectedContent)
            {
                try
                {
                    if (selectedContent.Type == "Link")
                    {
                        Process.Start(new ProcessStartInfo { FileName = selectedContent.Data, UseShellExecute = true });
                    }
                    else if (selectedContent.Type == "Document")
                    {
                        if (Uri.IsWellFormedUriString(selectedContent.Data, UriKind.Absolute))
                        {
                            string previewUrl = selectedContent.Data;

                            if (previewUrl.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
                                previewUrl.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                                previewUrl.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
                            {
                                previewUrl = $"https://docs.google.com/viewer?url={Uri.EscapeDataString(previewUrl)}";
                            }

                            Process.Start(new ProcessStartInfo
                            {
                                FileName = previewUrl,
                                UseShellExecute = true
                            });
                        }
                        else MessageBox.Show("Đường dẫn tệp không hợp lệ.", "Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show($"Không thể mở tệp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (item != null) DragDrop.DoDragDrop(dragHandle, item, DragDropEffects.Move);
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
                for (int i = 0; i < _courseContents.Count; i++) _courseContents[i].OrderIndex = i;

                try { await _dbManager.UpdateCourseContentOrderAsync(_course.Id, _courseContents.ToList()); }
                catch (Exception ex) { MessageBox.Show("Lỗi khi lưu thứ tự mới: " + ex.Message); LoadCourseContent(); }
            }
        }

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

                AddLinkInput.Text = ""; AddNoteInput.Text = "";

                if (_editingContent.Type == "Link") AddLinkInput.Text = _editingContent.Data;
                else if (_editingContent.Type == "Note") AddNoteInput.Text = _editingContent.Data;
                else if (_editingContent.Type == "Document")
                {
                    string displayTitle = GetDisplayNameFromUrl(_editingContent.Data, "document");
                    AddDocPathInput.Text = displayTitle;
                    AddDocPathInput.Tag = _editingContent.Data;
                }

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
                    if (_contentToDelete.Type == "Document" && _contentToDelete.Data.StartsWith("http"))
                    {
                        try
                        {
                            string publicId = GetPublicIdFromUrl(_contentToDelete.Data);
                            if (!string.IsNullOrEmpty(publicId))
                            {
                                await _cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Raw });
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine("Lỗi xóa Cloudinary: " + ex.Message); }
                    }
                    _courseContents.Remove(_contentToDelete);
                }
            }
            CloseDeleteContentModal_Click(null, null);
        }

        private string GetPublicIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                string path = Uri.UnescapeDataString(uri.AbsolutePath);

                int uploadIndex = path.IndexOf("/upload/");
                if (uploadIndex >= 0)
                {
                    string afterUpload = path.Substring(uploadIndex + 8);
                    int slashIndex = afterUpload.IndexOf("/");

                    if (slashIndex >= 0 && afterUpload.StartsWith("v") && char.IsDigit(afterUpload[1]))
                    {
                        return Path.GetFileName(afterUpload.Substring(slashIndex + 1));
                    }
                    return Path.GetFileName(afterUpload);
                }
                return Path.GetFileName(path);
            }
            catch { return string.Empty; }
        }

        private string GetDisplayNameFromUrl(string url, string prefixType)
        {
            string rawFileName = GetPublicIdFromUrl(url);
            string[] parts = rawFileName.Split('_');

            if (prefixType == "submission" && parts.Length >= 3)
            {
                return string.Join("_", parts.Skip(2));
            }
            else if (prefixType == "assignment" && parts.Length >= 2)
            {
                return string.Join("_", parts.Skip(1));
            }
            return rawFileName;
        }

        private void BtnAddContent_Click(object sender, RoutedEventArgs e)
        {
            _editingContent = null;
            TxtContentFormTitle.Text = "Thêm nội dung mới";
            BtnSubmitContent.Content = "Thêm vào lớp";
            AddTitleInput.Text = ""; AddLinkInput.Text = ""; AddNoteInput.Text = "";
            AddDocPathInput.Text = ""; AddDocPathInput.Tag = null;
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
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                AddDocPathInput.Text = openFileDialog.FileName;
                AddDocPathInput.Tag = openFileDialog.FileName;

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
                MessageBox.Show("Vui lòng nhập tiêu đề!"); return;
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
                string sourcePath = AddDocPathInput.Tag?.ToString();

                if (string.IsNullOrWhiteSpace(sourcePath)) { MessageBox.Show("Vui lòng chọn tệp hợp lệ!"); return; }

                if (!sourcePath.StartsWith("http") && File.Exists(sourcePath))
                {
                    try
                    {
                        BtnSubmitContent.Content = "Đang tải lên...";
                        BtnSubmitContent.IsEnabled = false;

                        var uploadParams = new RawUploadParams()
                        {
                            File = new FileDescription(sourcePath),
                            UseFilename = true,
                            UniqueFilename = true
                        };
                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);
                        data = uploadResult.SecureUrl.ToString();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi tải tệp lên đám mây: " + ex.Message);
                        BtnSubmitContent.Content = _editingContent != null ? "Lưu thay đổi" : "Thêm vào lớp";
                        BtnSubmitContent.IsEnabled = true; return;
                    }
                    finally
                    {
                        BtnSubmitContent.Content = _editingContent != null ? "Lưu thay đổi" : "Thêm vào lớp";
                        BtnSubmitContent.IsEnabled = true;
                    }
                }
                else { data = sourcePath; }
            }

            if (_editingContent != null)
            {
                if (_editingContent.Type == "Document" && _editingContent.Data != data && _editingContent.Data.StartsWith("http"))
                {
                    try
                    {
                        string exactPublicId = GetPublicIdFromUrl(_editingContent.Data);
                        if (!string.IsNullOrEmpty(exactPublicId))
                        {
                            await _cloudinary.DestroyAsync(new DeletionParams(exactPublicId) { ResourceType = ResourceType.Raw });
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine("Lỗi xóa Cloudinary: " + ex.Message); }
                }

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

        private async void LoadAssignments()
        {
            try
            {
                var query = _dbManager.GetDb.Collection("Courses").Document(_course.Id).Collection("Assignments");
                var snapshot = await query.GetSnapshotAsync();

                var list = new List<Assignment>();
                foreach (var doc in snapshot.Documents)
                {
                    var assign = doc.ConvertTo<Assignment>();
                    assign.Id = doc.Id;
                    assign.Deadline = assign.Deadline.ToLocalTime();
                    list.Add(assign);
                }

                _assignments = new ObservableCollection<Assignment>(list.OrderBy(a => a.Deadline));
                AssignmentsList.ItemsSource = _assignments;
                TxtAssignmentCount.Text = _assignments.Count.ToString();
            }
            catch (Exception ex) { Debug.WriteLine("Lỗi tải bài tập: " + ex.Message); }
        }

        private void BtnShowAddAssignment_Click(object sender, RoutedEventArgs e)
        {
            _editingAssignment = null;
            TxtAssignmentFormTitle.Text = "Tạo bài tập mới";
            BtnConfirmAddAssignment.Content = "Tạo bài tập";

            AssignTitleInput.Text = ""; AssignDescInput.Text = "";
            _selectedAssignFilePath = ""; TxtAssignSelectedFile.Text = "Kéo thả tệp vào đây hoặc nhấn để chọn";

            AssignDatePicker.SelectedDate = DateTime.Today.AddDays(7);
            AssignTimeInput.Text = "23:59";

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            AddAssignmentDrawer.Visibility = Visibility.Visible;
        }

        private void BtnEditAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string assignId)
            {
                _editingAssignment = _assignments.FirstOrDefault(a => a.Id == assignId);
                if (_editingAssignment == null) return;

                TxtAssignmentFormTitle.Text = "Chỉnh sửa bài tập";
                BtnConfirmAddAssignment.Content = "Lưu thay đổi";

                AssignTitleInput.Text = _editingAssignment.Title;
                AssignDescInput.Text = _editingAssignment.Description;
                AssignDatePicker.SelectedDate = _editingAssignment.Deadline.Date;
                AssignTimeInput.Text = _editingAssignment.Deadline.ToString("HH:mm");

                if (!string.IsNullOrEmpty(_editingAssignment.AttachedFileUrl))
                {
                    _selectedAssignFilePath = "";
                    string displayName = GetDisplayNameFromUrl(_editingAssignment.AttachedFileUrl, "assignment");
                    TxtAssignSelectedFile.Text = "Đã đính kèm: " + displayName;
                }
                else
                {
                    _selectedAssignFilePath = "";
                    TxtAssignSelectedFile.Text = "Kéo thả tệp vào đây hoặc nhấn để chọn";
                }

                MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                AddAssignmentDrawer.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string assignId)
            {
                _assignmentToDelete = _assignments.FirstOrDefault(a => a.Id == assignId);
                if (_assignmentToDelete != null)
                {
                    MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                    DeleteAssignmentOverlay.Visibility = Visibility.Visible;
                }
            }
        }

        private void CloseDeleteAssignmentModal_Click(object sender, RoutedEventArgs e)
        {
            DeleteAssignmentOverlay.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
            _assignmentToDelete = null;
        }

        private async void ConfirmDeleteAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (_assignmentToDelete != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_assignmentToDelete.AttachedFileUrl))
                    {
                        string oldPublicId = GetPublicIdFromUrl(_assignmentToDelete.AttachedFileUrl);
                        if (!string.IsNullOrEmpty(oldPublicId))
                        {
                            await _cloudinary.DestroyAsync(new DeletionParams($"assignments/{_course.Id}/{oldPublicId}") { ResourceType = ResourceType.Raw });
                        }
                    }

                    await _dbManager.GetDb.Collection("Courses").Document(_course.Id).Collection("Assignments").Document(_assignmentToDelete.Id).DeleteAsync();

                    if (_course.AssignmentCount > 0)
                    {
                        _course.AssignmentCount--;
                        await _dbManager.GetDb.Collection("Courses").Document(_course.Id).UpdateAsync("AssignmentCount", _course.AssignmentCount);
                    }

                    _assignments.Remove(_assignmentToDelete);
                    TxtAssignmentCount.Text = _course.AssignmentCount.ToString();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa bài tập: " + ex.Message);
                }
            }
            CloseDeleteAssignmentModal_Click(null, null);
        }

        private void CloseAddAssignmentDrawer_Click(object sender, RoutedEventArgs e)
        {
            AddAssignmentDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
            _editingAssignment = null;
        }

        private void AssignDropZone_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "All Files|*.*" };
            if (dialog.ShowDialog() == true)
            {
                _selectedAssignFilePath = dialog.FileName;
                TxtAssignSelectedFile.Text = "Đã chọn: " + Path.GetFileName(_selectedAssignFilePath);
            }
        }

        private void AssignDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _selectedAssignFilePath = files[0];
                    TxtAssignSelectedFile.Text = "Đã chọn: " + Path.GetFileName(_selectedAssignFilePath);
                }
            }
        }

        private async void ConfirmAddAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AssignTitleInput.Text) || !AssignDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng nhập Tên bài tập và Chọn ngày Hạn nộp!"); return;
            }

            DateTime deadlineDate = AssignDatePicker.SelectedDate.Value;
            if (!TimeSpan.TryParse(AssignTimeInput.Text, out TimeSpan time)) time = new TimeSpan(23, 59, 0);
            DateTime finalDeadline = deadlineDate.Add(time).ToUniversalTime();

            string fileUrl = "";

            BtnConfirmAddAssignment.Content = "Đang xử lý...";
            BtnConfirmAddAssignment.IsEnabled = false;

            if (!string.IsNullOrWhiteSpace(_selectedAssignFilePath) && File.Exists(_selectedAssignFilePath))
            {
                try
                {
                    string originalFileName = Path.GetFileNameWithoutExtension(_selectedAssignFilePath);
                    originalFileName = string.Join("_", originalFileName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                    string extension = Path.GetExtension(_selectedAssignFilePath);

                    string customPublicId = $"assignments/{_course.Id}/{DateTime.Now.Ticks}_{originalFileName}{extension}";

                    var uploadParams = new RawUploadParams()
                    {
                        File = new FileDescription(_selectedAssignFilePath),
                        PublicId = customPublicId,
                        UseFilename = true,
                        UniqueFilename = false
                    };
                    var res = await _cloudinary.UploadAsync(uploadParams);
                    fileUrl = res.SecureUrl.ToString();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi tải file đính kèm: " + ex.Message);
                    BtnConfirmAddAssignment.Content = _editingAssignment == null ? "Tạo bài tập" : "Lưu thay đổi";
                    BtnConfirmAddAssignment.IsEnabled = true; return;
                }
            }
            else
            {
                if (_editingAssignment != null) fileUrl = _editingAssignment.AttachedFileUrl;
            }

            try
            {
                var collectionRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id).Collection("Assignments");

                if (_editingAssignment != null)
                {
                    if (!string.IsNullOrEmpty(_editingAssignment.AttachedFileUrl) && _editingAssignment.AttachedFileUrl != fileUrl)
                    {
                        string oldPublicId = GetPublicIdFromUrl(_editingAssignment.AttachedFileUrl);
                        if (!string.IsNullOrEmpty(oldPublicId))
                            await _cloudinary.DestroyAsync(new DeletionParams($"assignments/{_course.Id}/{oldPublicId}") { ResourceType = ResourceType.Raw });
                    }

                    _editingAssignment.Title = AssignTitleInput.Text;
                    _editingAssignment.Description = AssignDescInput.Text;
                    _editingAssignment.Deadline = finalDeadline;
                    _editingAssignment.AttachedFileUrl = fileUrl;

                    await collectionRef.Document(_editingAssignment.Id).SetAsync(_editingAssignment, Google.Cloud.Firestore.SetOptions.MergeAll);
                }
                else
                {
                    var newAssignment = new Assignment
                    {
                        CourseId = _course.Id,
                        Title = AssignTitleInput.Text,
                        Description = AssignDescInput.Text,
                        Deadline = finalDeadline,
                        AttachedFileUrl = fileUrl,
                        CreatedAt = DateTime.UtcNow
                    };
                    await collectionRef.AddAsync(newAssignment);

                    _course.AssignmentCount++;
                    await _dbManager.GetDb.Collection("Courses").Document(_course.Id).UpdateAsync("AssignmentCount", _course.AssignmentCount);
                }

                TxtAssignmentCount.Text = _course.AssignmentCount.ToString();

                LoadAssignments();
                CloseAddAssignmentDrawer_Click(null, null);
            }
            catch (Exception ex) { MessageBox.Show("Lỗi lưu Database: " + ex.Message); }
            finally
            {
                BtnConfirmAddAssignment.Content = _editingAssignment == null ? "Tạo bài tập" : "Lưu thay đổi";
                BtnConfirmAddAssignment.IsEnabled = true;
            }
        }

        private async void AssignmentItem_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            _currentViewedAssignment = border?.DataContext as Assignment;

            if (_currentViewedAssignment != null)
            {
                DetailAssignTitle.Text = _currentViewedAssignment.Title;
                DetailAssignDeadline.Text = _currentViewedAssignment.Deadline.ToString("dd/MM/yyyy HH:mm");
                DetailAssignDesc.Text = string.IsNullOrWhiteSpace(_currentViewedAssignment.Description) ? "Không có mô tả thêm." : _currentViewedAssignment.Description;

                bool isLate = DateTime.Now > _currentViewedAssignment.Deadline;
                if (isLate)
                {
                    DetailAssignDeadline.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    DetailAssignDeadlineBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                    DetailAssignDeadlineBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA"));
                }
                else
                {
                    DetailAssignDeadline.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    DetailAssignDeadlineBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
                    DetailAssignDeadlineBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
                }

                if (string.IsNullOrEmpty(_currentViewedAssignment.AttachedFileUrl))
                {
                    BtnDetailDownloadAttach.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BtnDetailDownloadAttach.Visibility = Visibility.Visible;
                    BtnDetailDownloadAttach.Tag = _currentViewedAssignment.AttachedFileUrl;
                }

                var converter = new BrushConverter();
                Brush accentBrush = Brushes.SlateBlue;
                try { accentBrush = (SolidColorBrush)converter.ConvertFromString(_course.AccentColor); } catch { }
                BtnDetailSubmit.Background = accentBrush;
                DropZoneBorder.Stroke = accentBrush;
                BtnDownloadAll.Background = accentBrush;

                _selectedSubmissionFilePath = "";
                TxtSelectedFile.Text = "Kéo thả tệp vào đây hoặc nhấn để chọn";

                bool isInstructor = _course.InstructorId == CurrentUserId;

                StudentSubmitArea.Visibility = isInstructor ? Visibility.Collapsed : Visibility.Visible;
                InstructorManageArea.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;

                if (isInstructor)
                {
                    BtnDownloadAll.IsEnabled = false;
                    TxtSubmissionCount.Text = "Đang kiểm tra số lượng bài nộp...";
                    TxtInstructorGradingStatus.Text = "Trạng thái: Đang tải...";
                    try
                    {
                        var assignDoc = await _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                                 .Collection("Assignments").Document(_currentViewedAssignment.Id).GetSnapshotAsync();
                        bool isPublished = assignDoc.ContainsField("IsGradesPublished") && assignDoc.GetValue<bool>("IsGradesPublished");

                        var subsRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                             .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                             .Collection("Submissions");
                        var snap = await subsRef.GetSnapshotAsync();
                        int count = snap.Count;
                        TxtSubmissionCount.Text = $"Đã có {count} sinh viên nộp bài.";
                        BtnDownloadAll.IsEnabled = count > 0;

                        int gradedCount = snap.Documents.Count(d => d.ContainsField("Score") && d.GetValue<double?>("Score") != null);

                        if (isPublished)
                        {
                            TxtInstructorGradingStatus.Text = $"Trạng thái: Đã công khai ({gradedCount}/{count} bài có điểm)";
                            TxtInstructorGradingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                        }
                        else if (gradedCount > 0)
                        {
                            TxtInstructorGradingStatus.Text = $"Trạng thái: Đã chấm {gradedCount}/{count} bài (Chưa công khai)";
                            TxtInstructorGradingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                        }
                        else
                        {
                            TxtInstructorGradingStatus.Text = $"Trạng thái: Chưa chấm bài";
                            TxtInstructorGradingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                        }
                    }
                    catch { TxtSubmissionCount.Text = "Lỗi khi kiểm tra dữ liệu."; TxtInstructorGradingStatus.Text = "Trạng thái: Lỗi tải dữ liệu"; }
                }
                else
                {
                    DetailSubmissionStatus.Text = "Đang kiểm tra...";
                    DetailSubmissionStatus.Foreground = Brushes.Gray;
                    BtnDetailSubmit.Content = "Đang kiểm tra...";
                    BtnDetailSubmit.IsEnabled = false;
                    SubmittedFileDetailsPanel.Visibility = Visibility.Collapsed;
                    StudentGradePanel.Visibility = Visibility.Collapsed;

                    try
                    {
                        var assignDoc = await _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                                 .Collection("Assignments").Document(_currentViewedAssignment.Id).GetSnapshotAsync();
                        bool isPublished = assignDoc.ContainsField("IsGradesPublished") && assignDoc.GetValue<bool>("IsGradesPublished");

                        var subRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                             .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                             .Collection("Submissions").Document(CurrentUserId);
                        var snapshot = await subRef.GetSnapshotAsync();

                        if (snapshot.Exists)
                        {
                            var sub = snapshot.ConvertTo<Submission>();

                            DetailSubmissionStatus.Text = "Đã nộp bài";
                            DetailSubmissionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                            BtnDetailSubmit.Content = "Nộp lại tệp khác";

                            string displayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission");

                            TxtSubmittedFileName.Text = "Tệp: " + displayFileName;
                            TxtSubmittedTime.Text = "Thời gian nộp: " + sub.SubmittedAt.ToLocalTime().ToString("HH:mm - dd/MM/yyyy");

                            SubmittedFileDetailsPanel.Visibility = Visibility.Visible;

                            if (isPublished && snapshot.ContainsField("Score") && snapshot.GetValue<double?>("Score") != null)
                            {
                                StudentGradePanel.Visibility = Visibility.Visible;
                                TxtStudentScore.Text = $"{snapshot.GetValue<double>("Score")}/10";

                                string comment = snapshot.ContainsField("Comment") ? snapshot.GetValue<string>("Comment") : "";
                                TxtStudentComment.Text = string.IsNullOrWhiteSpace(comment) ? "Không có" : comment;

                                DropZoneGrid.Visibility = Visibility.Collapsed;
                                BtnDetailSubmit.Visibility = Visibility.Collapsed;
                                TxtFormatHint.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                DropZoneGrid.Visibility = Visibility.Visible;
                                BtnDetailSubmit.Visibility = Visibility.Visible;
                                TxtFormatHint.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            DetailSubmissionStatus.Text = "Chưa nộp";
                            DetailSubmissionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                            BtnDetailSubmit.Content = "Tải lên tệp bài làm";
                        }
                    }
                    catch
                    {
                        DetailSubmissionStatus.Text = "Không rõ";
                    }
                    BtnDetailSubmit.IsEnabled = true;
                }

                MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                AssignmentDetailDrawer.Visibility = Visibility.Visible;
            }
        }

        private void CloseAssignmentDetail_Click(object sender, RoutedEventArgs e)
        {
            AssignmentDetailDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
            _currentViewedAssignment = null;
        }

        private async void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentViewedAssignment == null) return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Lưu tệp nén chứa bài nộp",
                Filter = "ZIP Archive|*.zip",
                FileName = $"Submissions_{_currentViewedAssignment.Title.Replace(" ", "_")}.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                BtnDownloadAll.Content = "Đang chuẩn bị dữ liệu...";
                BtnDownloadAll.IsEnabled = false;

                try
                {
                    var subsRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                         .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                         .Collection("Submissions");
                    var snapshot = await subsRef.GetSnapshotAsync();

                    if (snapshot.Count == 0)
                    {
                        MessageBox.Show("Chưa có sinh viên nào nộp bài.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    using (FileStream zipToOpen = new FileStream(dialog.FileName, FileMode.Create))
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    using (HttpClient client = new HttpClient())
                    {
                        int currentIndex = 0;
                        foreach (var doc in snapshot.Documents)
                        {
                            currentIndex++;
                            BtnDownloadAll.Content = $"Đang tải ({currentIndex}/{snapshot.Count})...";

                            var sub = doc.ConvertTo<Submission>();
                            string displayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission");
                            string finalZipName = sub.IsLate ? $"late_{sub.StudentId}_{displayFileName}" : $"{sub.StudentId}_{displayFileName}";

                            var fileBytes = await client.GetByteArrayAsync(sub.FileUrl);

                            ZipArchiveEntry fileEntry = archive.CreateEntry(finalZipName);
                            using (Stream writer = fileEntry.Open())
                            {
                                await writer.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }
                        }
                    }

                    MessageBox.Show("Đã tải xuống thành công tất cả bài nộp!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi tải dữ liệu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    BtnDownloadAll.Content = "Tải về tất cả bài nộp (.zip)";
                    BtnDownloadAll.IsEnabled = true;
                }
            }
        }

        private async void BtnGradeAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentViewedAssignment == null) return;

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            GradingDrawer.Visibility = Visibility.Visible;
            GradingRightPanel.Visibility = Visibility.Collapsed;
            TxtGradingLoading.Visibility = Visibility.Visible;

            await LoadGradingListAsync();
        }

        private async Task LoadGradingListAsync()
        {
            try
            {
                var subsRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                     .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                     .Collection("Submissions");
                var snap = await subsRef.GetSnapshotAsync();

                _gradingList.Clear();

                foreach (var doc in snap.Documents)
                {
                    var sub = doc.ConvertTo<Submission>();
                    string sId = sub.StudentId;

                    var userDoc = await _dbManager.GetDb.Collection("Users").Document(sId).GetSnapshotAsync();
                    string name = userDoc.Exists ? userDoc.GetValue<string>("FullName") : "Học viên ẩn danh";

                    _gradingList.Add(new GradingItem
                    {
                        SubmissionId = doc.Id,
                        StudentId = sId,
                        FullName = name,
                        FileUrl = sub.FileUrl,
                        DisplayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission"),
                        SubmittedAt = sub.SubmittedAt.ToLocalTime(),
                        IsLate = sub.IsLate,
                        Score = doc.ContainsField("Score") ? doc.GetValue<double?>("Score") : null,
                        Comment = doc.ContainsField("Comment") ? doc.GetValue<string>("Comment") : ""
                    });
                }

                GradingItemsList.ItemsSource = null;
                GradingItemsList.ItemsSource = _gradingList;
                TxtGradingLoading.Visibility = Visibility.Collapsed;

                if (_gradingList.Count > 0)
                {
                    GradingItemsList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải danh sách chấm bài: " + ex.Message);
            }
        }

        private void GradingItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GradingItemsList.SelectedItem is GradingItem item)
            {
                _currentGradingItem = item;
                GradingRightPanel.Visibility = Visibility.Visible;

                TxtGradeStudentName.Text = item.FullName;
                TxtGradeStatus.Text = item.IsLate ? "Nộp trễ" : "Nộp đúng hạn";
                TxtGradeStatus.Foreground = item.IsLate ? Brushes.Red : Brushes.Green;
                TxtGradeFileName.Text = item.DisplayFileName;

                InputScore.Text = item.Score?.ToString() ?? "";
                InputComment.Text = item.Comment ?? "";
            }
            else
            {
                GradingRightPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnSaveAndNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGradingItem == null) return;

            double? score = null;
            if (!string.IsNullOrWhiteSpace(InputScore.Text))
            {
                if (double.TryParse(InputScore.Text, out double s) && s >= 0 && s <= 10)
                {
                    score = s;
                }
                else
                {
                    MessageBox.Show("Điểm phải là số từ 0 đến 10!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string comment = InputComment.Text;

            try
            {
                BtnSaveAndNext.IsEnabled = false;
                BtnSaveAndNext.Content = "Đang lưu...";

                var subRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                     .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                     .Collection("Submissions").Document(_currentGradingItem.SubmissionId);

                var updates = new Dictionary<string, object>
                {
                    { "Score", score },
                    { "Comment", comment }
                };

                await subRef.UpdateAsync(updates);

                _currentGradingItem.Score = score;
                _currentGradingItem.Comment = comment;
                GradingItemsList.Items.Refresh();

                int currentIndex = GradingItemsList.SelectedIndex;
                if (currentIndex >= 0 && currentIndex < _gradingList.Count - 1)
                {
                    GradingItemsList.SelectedIndex = currentIndex + 1;
                }
                else
                {
                    MessageBox.Show("Đã chấm xong bài cuối cùng trong danh sách!", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu điểm: " + ex.Message);
            }
            finally
            {
                BtnSaveAndNext.IsEnabled = true;
                BtnSaveAndNext.Content = "Lưu & Tiếp tục";
            }
        }

        private void CloseGradingDrawer_Click(object sender, RoutedEventArgs e)
        {
            GradingDrawer.Visibility = Visibility.Collapsed;
            _currentGradingItem = null;
        }

        private void BtnDownloadCurrentGradeFile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGradingItem != null && !string.IsNullOrEmpty(_currentGradingItem.FileUrl))
            {
                Process.Start(new ProcessStartInfo { FileName = _currentGradingItem.FileUrl, UseShellExecute = true });
            }
        }

        private async void BtnPublishGrades_Click(object sender, RoutedEventArgs e)
        {
            if (_currentViewedAssignment == null) return;

            var result = MessageBox.Show("Bạn có chắc chắn muốn công khai điểm cho toàn bộ sinh viên?\nSinh viên sẽ nhận được thông báo và xem được điểm ngay lập tức.", "Công khai điểm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var btn = sender as Button;
                    if (btn != null) { btn.Content = "Đang xử lý..."; btn.IsEnabled = false; }

                    var assignRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                         .Collection("Assignments").Document(_currentViewedAssignment.Id);

                    await assignRef.UpdateAsync("IsGradesPublished", true);

                    TxtInstructorGradingStatus.Text = "Trạng thái: Đã công khai";
                    TxtInstructorGradingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                    MessageBox.Show("Đã công khai điểm thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (btn != null) { btn.Content = "Công khai điểm"; btn.IsEnabled = true; }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi công khai điểm: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    var btn = sender as Button;
                    if (btn != null) { btn.Content = "Công khai điểm"; btn.IsEnabled = true; }
                }
            }
        }

        private void DownloadAttachedFile_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string url = btn?.Tag?.ToString();
            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Chọn tệp bài làm của bạn", Filter = "All Files|*.*" };
            if (dialog.ShowDialog() == true)
            {
                _selectedSubmissionFilePath = dialog.FileName;
                TxtSelectedFile.Text = "Đã chọn: " + Path.GetFileName(_selectedSubmissionFilePath);
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _selectedSubmissionFilePath = files[0];
                    TxtSelectedFile.Text = "Đã chọn: " + Path.GetFileName(_selectedSubmissionFilePath);
                }
            }
        }

        private async void BtnSubmitAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentViewedAssignment == null) return;

            if (string.IsNullOrEmpty(_selectedSubmissionFilePath) || !File.Exists(_selectedSubmissionFilePath))
            {
                MessageBox.Show("Vui lòng chọn hoặc kéo thả tệp bài làm của bạn vào ô nét đứt trước khi nộp!", "Chưa chọn tệp", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnDetailSubmit.Content = "Đang xử lý...";
            BtnDetailSubmit.IsEnabled = false;

            try
            {
                var subRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id)
                                     .Collection("Assignments").Document(_currentViewedAssignment.Id)
                                     .Collection("Submissions").Document(CurrentUserId);

                var snapshot = await subRef.GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    var oldSubmission = snapshot.ConvertTo<Submission>();
                    if (!string.IsNullOrEmpty(oldSubmission.FileUrl))
                    {
                        try
                        {
                            string exactPublicId = GetPublicIdFromUrl(oldSubmission.FileUrl);
                            if (!string.IsNullOrEmpty(exactPublicId))
                            {
                                string fullOldPublicId = $"submissions/{_course.Id}/{_currentViewedAssignment.Id}/{exactPublicId}";
                                await _cloudinary.DestroyAsync(new DeletionParams(fullOldPublicId) { ResourceType = ResourceType.Raw });
                            }
                        }
                        catch { }
                    }
                }

                bool isLate = DateTime.Now > _currentViewedAssignment.Deadline;

                string originalFileName = Path.GetFileNameWithoutExtension(_selectedSubmissionFilePath);
                originalFileName = string.Join("_", originalFileName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                string extension = Path.GetExtension(_selectedSubmissionFilePath);

                string customPublicId = $"submissions/{_course.Id}/{_currentViewedAssignment.Id}/{CurrentUserId}_{DateTime.Now.Ticks}_{originalFileName}{extension}";

                var uploadParams = new RawUploadParams()
                {
                    File = new FileDescription(_selectedSubmissionFilePath),
                    PublicId = customPublicId,
                    UseFilename = true,
                    UniqueFilename = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);

                var submission = new Submission
                {
                    AssignmentId = _currentViewedAssignment.Id,
                    StudentId = CurrentUserId,
                    FileUrl = uploadResult.SecureUrl.ToString(),
                    SubmittedAt = DateTime.UtcNow,
                    IsLate = isLate
                };

                await subRef.SetAsync(submission);

                DetailSubmissionStatus.Text = "Đã nộp bài";
                DetailSubmissionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                BtnDetailSubmit.Content = "Nộp lại tệp khác";

                SubmittedFileDetailsPanel.Visibility = Visibility.Visible;
                TxtSubmittedFileName.Text = "Tệp: " + Path.GetFileName(_selectedSubmissionFilePath);
                TxtSubmittedTime.Text = "Thời gian nộp: " + DateTime.Now.ToString("HH:mm - dd/MM/yyyy");

                _selectedSubmissionFilePath = "";
                TxtSelectedFile.Text = "Kéo thả tệp vào đây hoặc nhấn để chọn";

                string statusMsg = isLate ? "Đã cập nhật bài nộp (TRỄ HẠN) thành công!" : "Đã cập nhật bài nộp (ĐÚNG HẠN) thành công!";
                MessageBox.Show(statusMsg, "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi trong quá trình nộp bài: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnDetailSubmit.Content = DetailSubmissionStatus.Text == "Đã nộp bài" ? "Nộp lại tệp khác" : "Tải lên tệp bài làm";
            }
            finally
            {
                BtnDetailSubmit.IsEnabled = true;
            }
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            EditTitleInput.Text = _course.Title; EditDescInput.Text = _course.Description;
            EditClassInput.Text = _course.ClassName; EditEmojiInput.Text = _course.Emoji;
            EditCategoryInput.Text = _course.Category;
            SetComboBoxByContent(EditCbDay, _course.DayOfWeek);
            EditTxtStartPeriod.Text = _course.StartPeriod.ToString();
            EditTxtEndPeriod.Text = _course.EndPeriod.ToString();
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
                        if (item.ToString() == yearValue) { EditYearInput.SelectedItem = item; break; }
                }
            }
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 }; EditDrawer.Visibility = Visibility.Visible;
        }

        private void SetComboBoxByContent(ComboBox cb, string value)
        {
            foreach (ComboBoxItem item in cb.Items) if (item.Content.ToString() == value) { cb.SelectedItem = item; return; }
        }

        private async void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        {
            string day = (EditCbDay.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Hình thức 2";
            int startP = 0, endP = 0;

            if (day != "Hình thức 2")
            {
                if (!int.TryParse(EditTxtStartPeriod.Text, out startP) || !int.TryParse(EditTxtEndPeriod.Text, out endP) ||
                    startP >= endP || !((startP >= 1 && endP <= 5) || (startP >= 6 && endP <= 10)))
                {
                    MessageBox.Show("Tiết học không hợp lệ!\n- Tiết bắt đầu phải nhỏ hơn tiết kết thúc.\n- Cùng thuộc 1 buổi (Sáng: 1-5, Chiều: 6-10).", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _course.Title = EditTitleInput.Text; _course.Description = EditDescInput.Text;
            _course.ClassName = EditClassInput.Text; _course.Emoji = EditEmojiInput.Text;
            _course.Category = EditCategoryInput.Text; _course.AccentColor = GetSelectedColor();
            _course.CourseType = (EditTypeInput.SelectedItem as ComboBoxItem)?.Content.ToString();
            _course.DayOfWeek = day;
            _course.StartPeriod = startP;
            _course.EndPeriod = endP;
            string sem = (EditSemesterInput.SelectedItem as ComboBoxItem)?.Content.ToString();
            string year = EditYearInput.SelectedItem?.ToString();
            _course.Semester = $"{sem} - {year}";

            if (await _dbManager.UpdateCourseAsync(_course))
            {
                UpdateUI(); CloseEditDrawer_Click(null, null);
            }
        }

        private void CloseEditDrawer_Click(object sender, RoutedEventArgs e)
        {
            EditDrawer.Visibility = Visibility.Collapsed; MainScrollViewer.Effect = null;
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 }; DeleteOverlay.Visibility = Visibility.Visible;
        }

        private async void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (await _dbManager.DeleteCourseAsync(_course.Id)) NavigateBack();
        }

        private void CloseDeleteModal_Click(object sender, RoutedEventArgs e)
        {
            DeleteOverlay.Visibility = Visibility.Collapsed; MainScrollViewer.Effect = null;
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

        private void SetSelectedColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return;
            try
            {
                Color targetColor = (Color)ColorConverter.ConvertFromString(hexColor);

                foreach (var child in EditThemeColorsPanel.Children)
                {
                    if (child is RadioButton rb && rb.Background is SolidColorBrush brush)
                    {
                        if (brush.Color == targetColor)
                        {
                            rb.IsChecked = true;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private string GetSelectedColor()
        {
            foreach (var child in EditThemeColorsPanel.Children)
            {
                if (child is RadioButton rb && rb.IsChecked == true && rb.Background is SolidColorBrush brush)
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

        private void UserControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickedElement = e.OriginalSource as DependencyObject;
            var clickedItem = FindVisualParent<ListBoxItem>(clickedElement);
            if (clickedItem == null) DocumentsList.SelectedItem = null;
        }

        public static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = child;
            while (parentObject != null)
            {
                if (parentObject is T parent) return parent;
                if (parentObject is FrameworkContentElement contentElement) parentObject = contentElement.Parent;
                else parentObject = VisualTreeHelper.GetParent(parentObject);
            }
            return null;
        }

        private ObservableCollection<PendingRequest> _pendingRequests = new();

        private async void BtnManageApprovals_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            ApprovalDrawer.Visibility = Visibility.Visible;
            await LoadPendingRequestsAsync();
        }

        private void CloseApprovalDrawer_Click(object sender, RoutedEventArgs e)
        {
            ApprovalDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private async Task LoadPendingRequestsAsync()
        {
            try
            {
                var snap = await _dbManager.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", _course.Id)
                    .WhereEqualTo("status", "pending")
                    .GetSnapshotAsync();

                _pendingRequests.Clear();

                foreach (var doc in snap.Documents)
                {
                    string uId = doc.GetValue<string>("userId");

                    var userDoc = await _dbManager.GetDb.Collection("Users").Document(uId).GetSnapshotAsync();
                    string name = userDoc.Exists ? userDoc.GetValue<string>("FullName") : "Học viên ẩn danh";
                    string email = userDoc.Exists ? userDoc.GetValue<string>("Email") : "";

                    DateTime requestedAt = DateTime.Now;
                    if (doc.ContainsField("createdAt"))
                    {
                        requestedAt = doc.GetValue<Google.Cloud.Firestore.Timestamp>("createdAt").ToDateTime().ToLocalTime();
                    }
                    else if (doc.ContainsField("requestDate"))
                    {
                        requestedAt = doc.GetValue<Google.Cloud.Firestore.Timestamp>("requestDate").ToDateTime().ToLocalTime();
                    }

                    _pendingRequests.Add(new PendingRequest
                    {
                        RegistrationId = doc.Id,
                        UserId = uId,
                        FullName = name,
                        Email = email,
                        RequestedAt = requestedAt
                    });
                }

                PendingRequestsList.ItemsSource = _pendingRequests;

                if (_pendingRequests.Count > 0)
                {
                    TxtNoPendingRequests.Visibility = Visibility.Collapsed;
                    BtnAcceptAll.Visibility = Visibility.Visible;

                    BadgeBorder.Visibility = Visibility.Visible;
                    TxtPendingCount.Text = _pendingRequests.Count > 99 ? "99+" : _pendingRequests.Count.ToString();
                }
                else
                {
                    TxtNoPendingRequests.Visibility = Visibility.Visible;
                    BtnAcceptAll.Visibility = Visibility.Collapsed;
                    BadgeBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải danh sách: " + ex.Message);
            }
        }

        private async void BtnAcceptStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                try
                {
                    var regRef = _dbManager.GetDb.Collection("courseRegistrations").Document(regId);

                    await regRef.UpdateAsync(new Dictionary<string, object> {
                        { "status", "accepted" },
                        { "approvedDate", Google.Cloud.Firestore.FieldValue.ServerTimestamp }
                    });

                    _course.StudentCount++;
                    await _dbManager.GetDb.Collection("Courses").Document(_course.Id).UpdateAsync("StudentCount", _course.StudentCount);

                    TxtStudentCount.Text = _course.StudentCount.ToString();
                    await LoadPendingRequestsAsync();
                }
                catch (Exception ex) { MessageBox.Show("Lỗi duyệt: " + ex.Message); }
            }
        }

        private async void BtnRejectStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                try
                {
                    var regRef = _dbManager.GetDb.Collection("courseRegistrations").Document(regId);
                    await regRef.UpdateAsync("status", "rejected");
                    await LoadPendingRequestsAsync();
                }
                catch (Exception ex) { MessageBox.Show("Lỗi từ chối: " + ex.Message); }
            }
        }

        private async void BtnAcceptAll_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingRequests.Count == 0) return;

            BtnAcceptAll.Content = "Đang xử lý...";
            BtnAcceptAll.IsEnabled = false;

            try
            {
                Google.Cloud.Firestore.WriteBatch batch = _dbManager.GetDb.StartBatch();

                foreach (var req in _pendingRequests)
                {
                    var regRef = _dbManager.GetDb.Collection("courseRegistrations").Document(req.RegistrationId);
                    batch.Update(regRef, "status", "accepted");
                    batch.Update(regRef, "approvedDate", Google.Cloud.Firestore.FieldValue.ServerTimestamp);
                }

                _course.StudentCount += _pendingRequests.Count;
                var courseRef = _dbManager.GetDb.Collection("Courses").Document(_course.Id);
                batch.Update(courseRef, "StudentCount", _course.StudentCount);

                await batch.CommitAsync();

                TxtStudentCount.Text = _course.StudentCount.ToString();
                await LoadPendingRequestsAsync();

                MessageBox.Show("Đã duyệt tất cả yêu cầu thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi duyệt tất cả: " + ex.Message);
            }
            finally
            {
                BtnAcceptAll.Content = "Duyệt tất cả";
                BtnAcceptAll.IsEnabled = true;
            }
        }

        private ObservableCollection<EnrolledStudent> _enrolledStudents = new();

        private async void BtnManageStudents_Click(object sender, RoutedEventArgs e)
        {
            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            ManageStudentsDrawer.Visibility = Visibility.Visible;
            await LoadEnrolledStudentsAsync();
        }

        private void CloseManageStudentsDrawer_Click(object sender, RoutedEventArgs e)
        {
            ManageStudentsDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private async Task LoadEnrolledStudentsAsync()
        {
            try
            {
                var snap = await _dbManager.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", _course.Id)
                    .WhereEqualTo("status", "accepted")
                    .GetSnapshotAsync();

                _enrolledStudents.Clear();

                foreach (var doc in snap.Documents)
                {
                    string uId = doc.GetValue<string>("userId");

                    var userDoc = await _dbManager.GetDb.Collection("Users").Document(uId).GetSnapshotAsync();
                    string name = userDoc.Exists ? userDoc.GetValue<string>("FullName") : "Học viên ẩn danh";
                    string email = userDoc.Exists ? userDoc.GetValue<string>("Email") : "";

                    DateTime approvedDate = DateTime.Now;
                    if (doc.ContainsField("approvedDate"))
                    {
                        approvedDate = doc.GetValue<Google.Cloud.Firestore.Timestamp>("approvedDate").ToDateTime().ToLocalTime();
                    }

                    _enrolledStudents.Add(new EnrolledStudent
                    {
                        RegistrationId = doc.Id,
                        UserId = uId,
                        FullName = name,
                        Email = email,
                        ApprovedDate = approvedDate
                    });
                }

                EnrolledStudentsList.ItemsSource = _enrolledStudents;
                TxtNoEnrolledStudents.Visibility = _enrolledStudents.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

                _course.StudentCount = _enrolledStudents.Count;
                TxtStudentCount.Text = _course.StudentCount.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải danh sách sinh viên: " + ex.Message);
            }
        }

        private async void BtnRemoveStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                var result = MessageBox.Show("Bạn có chắc chắn muốn loại bỏ sinh viên này khỏi lớp? Trạng thái sẽ được chuyển thành 'Từ chối'.", "Xác nhận loại bỏ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    btn.IsEnabled = false;

                    try
                    {
                        var regRef = _dbManager.GetDb.Collection("courseRegistrations").Document(regId);
                        await regRef.UpdateAsync(new Dictionary<string, object> {
                    { "status", "rejected" },
                    { "removedDate", Google.Cloud.Firestore.FieldValue.ServerTimestamp }
                });

                        if (_course.StudentCount > 0)
                        {
                            _course.StudentCount--;
                        }

                        await _dbManager.GetDb.Collection("Courses").Document(_course.Id).UpdateAsync("StudentCount", _course.StudentCount);

                        TxtStudentCount.Text = _course.StudentCount.ToString();

                        await LoadEnrolledStudentsAsync();

                        MessageBox.Show("Đã loại bỏ sinh viên và chặn quyền truy cập thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi xử lý: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        btn.IsEnabled = true;
                    }
                }
            }
        }

        private void EditCbDay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditCbDay.SelectedItem is ComboBoxItem item && EditTxtStartPeriod != null && EditTxtEndPeriod != null)
            {
                if (item.Content.ToString() == "Hình thức 2")
                {
                    EditTxtStartPeriod.Text = "0";
                    EditTxtEndPeriod.Text = "0";
                    EditTxtStartPeriod.IsEnabled = false;
                    EditTxtEndPeriod.IsEnabled = false;
                    EditTxtStartPeriod.Opacity = 0.5;
                    EditTxtEndPeriod.Opacity = 0.5;
                }
                else
                {
                    EditTxtStartPeriod.IsEnabled = true;
                    EditTxtEndPeriod.IsEnabled = true;
                    EditTxtStartPeriod.Opacity = 1;
                    EditTxtEndPeriod.Opacity = 1;
                }
            }
        }
    }
}