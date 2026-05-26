using e_learning_app;
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
        private ObservableCollection<Lesson> _lessons;

        private CourseContent _editingContent = null;
        private CourseContent _contentToDelete = null;

        private Assignment _currentViewedAssignment = null;
        private string _selectedSubmissionFilePath = "";

        private Assignment _editingAssignment = null;
        private Assignment _assignmentToDelete = null;

        private Lesson _editingLesson = null;
        private Lesson _lessonToDelete = null;

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
            LoadAssignmentsAsync();
            LoadLessonsAsync();
            UpdateUI();

            this.Unloaded += (s, e) =>
            {
                
                
                
                
            };
        }

        private void InitializeYearComboBox()
        {
            int currentYear = DateTime.Now.Year;
            EditYearInput.Items.Clear();
            for (int i = currentYear - 2; i <= currentYear + 3; i++)
            {
                EditYearInput.Items.Add($"{i} - {i + 1}");
            }
        }

        private async void ApplyRolePermissions()
        {
            bool isInstructor = _course.InstructorId == CurrentUserId;
            BtnMoreActions.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnAddContent.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
            BtnAddVideo.Visibility = isInstructor ? Visibility.Visible : Visibility.Collapsed;
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
                var regs = await ApiService.GetAsync<List<RegistrationResponse>>($"courses/{_course.Id}/students");
                if (regs != null)
                {
                    int pendingCount = regs.Count(r => r.Data.status?.ToLower() == "pending");
                    int enrolledCount = regs.Count(r => r.Data.status?.ToLower() == "active" || r.Data.status?.ToLower() == "accepted");

                    if (_course.InstructorId == CurrentUserId && _course.StudentCount != enrolledCount)
                    {
                        _course.StudentCount = enrolledCount;
                        await _dbManager.UpdateCourseAsync(_course);
                        TxtStudentCount.Text = enrolledCount.ToString();
                    }

                    if (pendingCount > 0)
                    {
                        BadgeBorder.Visibility = Visibility.Visible;
                        TxtPendingCount.Text = pendingCount > 99 ? "99+" : pendingCount.ToString();
                    }
                    else
                    {
                        BadgeBorder.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch { }
        }

        private async void LoadCourseContent()
        {
            try
            {
                var response = await ApiService.GetAsync<List<ContentResponse>>($"courses/{_course.Id}/contents");
                if (response != null)
                {
                    var contents = new List<CourseContent>();
                    foreach (var resp in response)
                    {
                        if (resp.Data != null)
                        {
                            var content = resp.Data;
                            content.Id = resp.Id;
                            contents.Add(content);
                        }
                    }
                    _courseContents = new ObservableCollection<CourseContent>(contents.OrderBy(c => c.OrderIndex));
                    DocumentsList.ItemsSource = _courseContents;
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tải nội dung: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }

        private async void LoadLessonsAsync()
        {
            try
            {
                var response = await ApiService.GetAsync<List<LessonResponse>>($"courses/{_course.Id}/lessons");
                if (response != null)
                {
                    var lessons = new List<Lesson>();
                    foreach (var resp in response)
                    {
                        if (resp.Data != null)
                        {
                            var lesson = resp.Data;
                            lesson.Id = resp.Id;
                            lesson.CreatedAt = lesson.CreatedAt.ToLocalTime();
                            lessons.Add(lesson);
                        }
                    }
                    _lessons = new ObservableCollection<Lesson>(lessons.OrderBy(l => l.CreatedAt));
                    LessonsList.ItemsSource = _lessons;
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tải danh sách video: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }


        private void BtnAddVideo_Click(object sender, RoutedEventArgs e)
        {
            InputVideoTitle.Text = "";
            InputVideoDesc.Text = "";
            TxtSelectedVideoFile.Tag = null;
            MainScrollViewer.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 10 };
            AddVideoDrawer.Visibility = Visibility.Visible;
        }

        private void CloseAddVideoDrawer_Click(object sender, RoutedEventArgs e)
        {
            AddVideoDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private void BtnBrowseVideo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                TxtSelectedVideoFile.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
                TxtSelectedVideoFile.Tag = openFileDialog.FileName;

                if (string.IsNullOrWhiteSpace(InputVideoTitle.Text))
                {
                    InputVideoTitle.Text = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                }
            }
        }

        private async void ConfirmAddVideo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputVideoTitle.Text))
            {
                CustomDialog.Show("Vui lòng nhập tiêu đề video!", "Cảnh báo", DialogType.Warning);
                return;
            }

            string sourcePath = TxtSelectedVideoFile.Tag?.ToString();
            string videoUrl = _editingLesson?.VideoUrl ?? ""; // Nếu đang sửa thì giữ URL cũ

            BtnConfirmAddVideo.Content = "Đang xử lý...";
            BtnConfirmAddVideo.IsEnabled = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath) && System.IO.File.Exists(sourcePath))
                {
                    if (_editingLesson != null && !string.IsNullOrEmpty(_editingLesson.VideoUrl))
                    {
                        string oldPublicId = GetPublicIdFromUrl(_editingLesson.VideoUrl);
                        if (!string.IsNullOrEmpty(oldPublicId))
                            await _cloudinary.DestroyAsync(new DeletionParams(oldPublicId) { ResourceType = ResourceType.Video });
                    }

                    var uploadParams = new VideoUploadParams()
                    {
                        File = new FileDescription(sourcePath),
                        UseFilename = true,
                        UniqueFilename = true
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);
                    videoUrl = uploadResult.SecureUrl.ToString();
                }
                else if (_editingLesson == null)
                {
                    CustomDialog.Show("Vui lòng chọn tệp video!", "Cảnh báo", DialogType.Warning);
                    BtnConfirmAddVideo.IsEnabled = true;
                    BtnConfirmAddVideo.Content = "Tải lên Video";
                    return;
                }

                if (_editingLesson != null)
                {
                    _editingLesson.Title = InputVideoTitle.Text;
                    _editingLesson.Description = InputVideoDesc.Text;
                    _editingLesson.VideoUrl = videoUrl;

                    var req = new
                    {
                        Title = _editingLesson.Title,
                        Description = _editingLesson.Description,
                        VideoUrl = _editingLesson.VideoUrl
                    };
                    var response = await ApiService.PutAsync($"courses/{_course.Id}/lessons/{_editingLesson.Id}", req);
                    if (response != null)
                    {
                        int index = _lessons.IndexOf(_editingLesson);
                        _lessons.RemoveAt(index);
                        _lessons.Insert(index, _editingLesson);
                        CustomDialog.Show("Cập nhật video thành công!", "Thành công", DialogType.Success);
                    }
                }
                else
                {
                    var req = new
                    {
                        Title = InputVideoTitle.Text,
                        Description = InputVideoDesc.Text,
                        VideoUrl = videoUrl
                    };

                    var response = await ApiService.PostAsync<object, LessonResponse>($"courses/{_course.Id}/lessons", req);
                    if (response != null && response.Data != null)
                    {
                        var newLesson = response.Data;
                        newLesson.Id = response.Id;
                        
                        if (_lessons == null) _lessons = new ObservableCollection<Lesson>();
                        _lessons.Add(newLesson);
                        await NotificationService.SendToClassAsync(_dbManager, _course.Id, "Video bài giảng mới", $"Giáo viên vừa tải lên video: {newLesson.Title}", "Course", CurrentUserId, "Giáo viên");
                        CustomDialog.Show("Tải video lên thành công!", "Thành công", DialogType.Success);
                    }
                }
                LoadLessonsAsync();
                CloseAddVideoDrawer_Click(null, null);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi: " + ex.Message, "Lỗi", DialogType.Error);
            }
            finally
            {
                BtnConfirmAddVideo.Content = "Tải lên Video";
                BtnConfirmAddVideo.IsEnabled = true;
                _editingLesson = null;
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
                var response = await ApiService.GetAsync<UserResponse>($"users/{_course.InstructorId}");
                if (response != null && response.Data != null)
                {
                    var user = response.Data;
                    TxtInstructorName.Text = !string.IsNullOrEmpty(user.FullName) ? user.FullName : "Không xác định";
                    TxtInstructorEmail.Text = !string.IsNullOrEmpty(user.Email) ? user.Email : "N/A";
                    TxtInstructorPhone.Text = !string.IsNullOrEmpty(user.PhoneNumber) ? user.PhoneNumber : "N/A";
                }
                else
                {
                    TxtInstructorName.Text = "Không xác định";
                    TxtInstructorEmail.Text = "N/A";
                    TxtInstructorPhone.Text = "N/A";
                }
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
                        else CustomDialog.Show("Đường dẫn tệp không hợp lệ.", "Lỗi Mạng", DialogType.Warning);
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
                    CustomDialog.Show($"Không thể mở tệp: {ex.Message}", "Lỗi", DialogType.Error);
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

                try
                {
                    var tasks = new List<Task>();
                    foreach (var content in _courseContents)
                    {
                        var req = new
                        {
                            Title = content.Title,
                            Type = content.Type,
                            Data = content.Data,
                            OrderIndex = content.OrderIndex
                        };
                        tasks.Add(ApiService.PutAsync($"courses/{_course.Id}/contents/{content.Id}", req));
                    }
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    CustomDialog.Show("Lỗi khi lưu thứ tự mới: " + ex.Message, "Lỗi", DialogType.Error);
                    LoadCourseContent();
                }
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
                var response = await ApiService.DeleteAsync($"courses/{_course.Id}/contents/{_contentToDelete.Id}");
                if (response)
                {
                    _courseContents.Remove(_contentToDelete);
                    _contentToDelete = null;
                }
            }
            else if (_lessonToDelete != null)
            {
                var response = await ApiService.DeleteAsync($"courses/{_course.Id}/lessons/{_lessonToDelete.Id}");
                if (response)
                {
                    try
                    {
                        string publicId = GetPublicIdFromUrl(_lessonToDelete.VideoUrl);
                        if (!string.IsNullOrEmpty(publicId))
                        {
                            await _cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Video });
                        }
                    }
                    catch { }

                    _lessons.Remove(_lessonToDelete);
                    _lessonToDelete = null;
                    CustomDialog.Show("Đã xóa video thành công!", "Thông báo", DialogType.Success);
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
            AddTitleInput.Text = "";
            AddLinkInput.Text = "";
            AddNoteInput.Text = "";
            AddDocPathInput.Tag = null;
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
                CustomDialog.Show("Vui lòng nhập tiêu đề!", "Cảnh báo", DialogType.Warning); return;
            }

            string type = (AddTypeInput.SelectedItem as ComboBoxItem)?.Content.ToString();
            string data = "";

            if (type == "Link")
            {
                data = AddLinkInput.Text;
                if (string.IsNullOrWhiteSpace(data)) { CustomDialog.Show("Vui lòng nhập link!", "Cảnh báo", DialogType.Warning); return; }
            }
            else if (type == "Note")
            {
                data = AddNoteInput.Text;
                if (string.IsNullOrWhiteSpace(data)) { CustomDialog.Show("Vui lòng nhập ghi chú!", "Cảnh báo", DialogType.Warning); return; }
            }
            else if (type == "Document")
            {
                string sourcePath = AddDocPathInput.Tag?.ToString();

                if (string.IsNullOrWhiteSpace(sourcePath)) { CustomDialog.Show("Vui lòng chọn tệp hợp lệ!", "Cảnh báo", DialogType.Warning); return; }

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
                        CustomDialog.Show("Lỗi khi tải tệp lên đám mây: " + ex.Message, "Lỗi", DialogType.Error);
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

                var req = new
                {
                    Title = _editingContent.Title,
                    Type = _editingContent.Type,
                    Data = _editingContent.Data,
                    OrderIndex = _editingContent.OrderIndex
                };
                var response = await ApiService.PutAsync($"courses/{_course.Id}/contents/{_editingContent.Id}", req);
                if (response != null)
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
                
                var req = new
                {
                    Title = AddTitleInput.Text,
                    Type = type,
                    Data = data,
                    OrderIndex = nextOrderIndex
                };
                
                var response = await ApiService.PostAsync<object, ContentResponse>($"courses/{_course.Id}/contents", req);
                if (response != null && response.Data != null)
                {
                    var newContent = response.Data;
                    newContent.Id = response.Id;
                    
                    if (_courseContents == null) _courseContents = new ObservableCollection<CourseContent>();
                    _courseContents.Add(newContent);
                    await NotificationService.SendToClassAsync(_dbManager, _course.Id, "Tài liệu mới", $"Giáo viên vừa thêm tài liệu: {newContent.Title}", "Course", CurrentUserId, "Giáo viên");
                }
            }
            LoadCourseContent();
            CloseAddDrawer_Click(null, null);
        }

        private async void LoadAssignmentsAsync()
        {
            try
            {
                var response = await ApiService.GetAsync<List<AssignmentResponse>>($"courses/{_course.Id}/assignments");
                if (response != null)
                {
                    var list = new List<Assignment>();
                    foreach (var resp in response)
                    {
                        if (resp.Data != null)
                        {
                            var assign = resp.Data;
                            assign.Id = resp.Id;
                            assign.Deadline = assign.Deadline.ToLocalTime();
                            list.Add(assign);
                        }
                    }
                    _assignments = new ObservableCollection<Assignment>(list.OrderBy(a => a.Deadline));
                    AssignmentsList.ItemsSource = _assignments;
                    TxtAssignmentCount.Text = _assignments.Count.ToString();
                    
                    if (_course.InstructorId == CurrentUserId && _course.AssignmentCount != _assignments.Count)
                    {
                        _course.AssignmentCount = _assignments.Count;
                        await _dbManager.UpdateCourseAsync(_course);
                    }
                }
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

                    await ApiService.DeleteAsync($"courses/{_course.Id}/assignments/{_assignmentToDelete.Id}");

                    if (_course.AssignmentCount > 0)
                    {
                        _course.AssignmentCount--;
                        await _dbManager.UpdateCourseAsync(_course);
                    }

                    _assignments.Remove(_assignmentToDelete);
                    TxtAssignmentCount.Text = _course.AssignmentCount.ToString();
                }
                catch (Exception ex)
                {
                    CustomDialog.Show("Lỗi khi xóa bài tập: " + ex.Message, "Lỗi", DialogType.Error);
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
                CustomDialog.Show("Vui lòng nhập Tên bài tập và Chọn ngày Hạn nộp!", "Cảnh báo", DialogType.Warning); return;
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
                    CustomDialog.Show("Lỗi tải file đính kèm: " + ex.Message, "Lỗi", DialogType.Error);
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
                if (_editingAssignment != null)
                {
                    if (!string.IsNullOrEmpty(_editingAssignment.AttachedFileUrl) && _editingAssignment.AttachedFileUrl != fileUrl)
                    {
                        string oldPublicId = GetPublicIdFromUrl(_editingAssignment.AttachedFileUrl);
                        if (!string.IsNullOrEmpty(oldPublicId))
                            await _cloudinary.DestroyAsync(new DeletionParams($"assignments/{_course.Id}/{oldPublicId}") { ResourceType = ResourceType.Raw });
                    }

                    var updateReq = new
                    {
                        Title = AssignTitleInput.Text,
                        Description = AssignDescInput.Text,
                        Deadline = finalDeadline,
                        AttachedFileUrl = fileUrl
                    };

                    await ApiService.PutAsync($"courses/{_course.Id}/assignments/{_editingAssignment.Id}", updateReq);
                }
                else
                {
                    var newAssignmentReq = new
                    {
                        Title = AssignTitleInput.Text,
                        Description = AssignDescInput.Text,
                        DueDate = finalDeadline,
                        AttachedFileUrl = fileUrl
                    };
                    await ApiService.PostAsync($"courses/{_course.Id}/assignments", newAssignmentReq);

                    _course.AssignmentCount++;
                    await _dbManager.UpdateCourseAsync(_course);
                    await NotificationService.SendToClassAsync(_dbManager, _course.Id, "Bài tập mới", $"Giáo viên vừa tạo bài tập: {newAssignmentReq.Title}", "Homework");
                }

                TxtAssignmentCount.Text = _course.AssignmentCount.ToString();

                LoadAssignmentsAsync();
                CloseAddAssignmentDrawer_Click(null, null);
            }
            catch (Exception ex) { CustomDialog.Show("Lỗi lưu Database: " + ex.Message, "Lỗi", DialogType.Error); }
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
                        var assignDoc = await ApiService.GetAsync<AssignmentResponse>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}");
                        bool isPublished = assignDoc != null && assignDoc.Data != null && assignDoc.Data.IsGradesPublished;

                        var submissions = await ApiService.GetAsync<List<SubmissionResponse>>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions");
                        
                        int count = submissions?.Count ?? 0;
                        TxtSubmissionCount.Text = $"Đã có {count} sinh viên nộp bài.";
                        BtnDownloadAll.IsEnabled = count > 0;

                        int gradedCount = submissions?.Count(d => d.Data != null && d.Data.Score.HasValue) ?? 0;

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
                        var assignDoc = await ApiService.GetAsync<AssignmentResponse>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}");
                        bool isPublished = assignDoc != null && assignDoc.Data != null && assignDoc.Data.IsGradesPublished;

                        // Because the API doesn't have an endpoint for a single submission, we'll fetch all submissions and find the student's submission
                        var submissions = await ApiService.GetAsync<List<SubmissionResponse>>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions");
                        var mySubmissionResp = submissions?.FirstOrDefault(s => s.Id == CurrentUserId);

                        if (mySubmissionResp != null && mySubmissionResp.Data != null)
                        {
                            var sub = mySubmissionResp.Data;

                            DetailSubmissionStatus.Text = "Đã nộp bài";
                            DetailSubmissionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                            BtnDetailSubmit.Content = "Nộp lại tệp khác";

                            string displayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission");

                            TxtSubmittedFileName.Text = "Tệp: " + displayFileName;
                            TxtSubmittedTime.Text = "Thời gian nộp: " + sub.SubmittedAt.ToLocalTime().ToString("HH:mm - dd/MM/yyyy");

                            SubmittedFileDetailsPanel.Visibility = Visibility.Visible;

                            if (isPublished && sub.Score.HasValue)
                            {
                                StudentGradePanel.Visibility = Visibility.Visible;
                                TxtStudentScore.Text = $"{sub.Score.Value}/10";

                                string comment = sub.Comment ?? "";
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
                    var submissions = await ApiService.GetAsync<List<SubmissionResponse>>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions");

                    if (submissions == null || submissions.Count == 0)
                    {
                        CustomDialog.Show("Chưa có sinh viên nào nộp bài.", "Thông báo", DialogType.Info);
                        return;
                    }

                    using (FileStream zipToOpen = new FileStream(dialog.FileName, FileMode.Create))
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    using (HttpClient client = new HttpClient())
                    {
                        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        int currentIndex = 0;
                        foreach (var resp in submissions)
                        {
                            if (resp.Data == null || string.IsNullOrEmpty(resp.Data.FileUrl)) continue;
                            
                            currentIndex++;
                            BtnDownloadAll.Content = $"Đang tải ({currentIndex}/{submissions.Count})...";

                            var sub = resp.Data;
                            string displayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission");
                            if (string.IsNullOrEmpty(displayFileName))
                            {
                                displayFileName = "bai_nop";
                            }

                            // Làm sạch tên file để tránh các ký tự không hợp lệ trong tệp ZIP
                            string cleanFileName = string.Join("_", displayFileName.Split(Path.GetInvalidFileNameChars()));
                            string finalZipName = sub.IsLate ? $"late_{sub.StudentId}_{cleanFileName}" : $"{sub.StudentId}_{cleanFileName}";

                            // Đảm bảo không trùng tên file trong ZIP để tránh ngoại lệ ArgumentException
                            if (addedNames.Contains(finalZipName))
                            {
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(finalZipName);
                                string ext = Path.GetExtension(finalZipName);
                                int suffix = 1;
                                while (addedNames.Contains($"{nameWithoutExt}_{suffix}{ext}"))
                                {
                                    suffix++;
                                }
                                finalZipName = $"{nameWithoutExt}_{suffix}{ext}";
                            }
                            addedNames.Add(finalZipName);

                            try
                            {
                                var fileBytes = await client.GetByteArrayAsync(sub.FileUrl);
                                ZipArchiveEntry fileEntry = archive.CreateEntry(finalZipName);
                                using (Stream writer = fileEntry.Open())
                                {
                                    await writer.WriteAsync(fileBytes, 0, fileBytes.Length);
                                }
                            }
                            catch (Exception downloadEx)
                            {
                                // Ghi tệp thông báo lỗi thay vì làm sập toàn bộ tiến trình tải xuống
                                string errorFileName = sub.IsLate ? $"error_late_{sub.StudentId}.txt" : $"error_{sub.StudentId}.txt";
                                if (addedNames.Contains(errorFileName))
                                {
                                    errorFileName = $"error_{sub.StudentId}_{currentIndex}.txt";
                                }
                                addedNames.Add(errorFileName);

                                ZipArchiveEntry errorEntry = archive.CreateEntry(errorFileName);
                                using (Stream writer = errorEntry.Open())
                                using (StreamWriter textWriter = new StreamWriter(writer))
                                {
                                    await textWriter.WriteLineAsync($"Lỗi tải tệp: {downloadEx.Message}");
                                    await textWriter.WriteLineAsync($"Đường dẫn gốc: {sub.FileUrl}");
                                }
                            }
                        }
                    }

                    CustomDialog.Show("Đã tải xuống thành công tất cả bài nộp!", "Thành công", DialogType.Success);
                }
                catch (Exception ex)
                {
                    CustomDialog.Show("Lỗi khi tải dữ liệu: " + ex.Message, "Lỗi", DialogType.Error);
                }
                finally
                {
                    BtnDownloadAll.Content = "Tải về tất cả bài nộp (.zip)";
                    BtnDownloadAll.IsEnabled = true;
                }
            }
        }

        private void BtnGradeAssignment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentViewedAssignment == null) return;

            MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
            GradingDrawer.Visibility = Visibility.Visible;
            GradingRightPanel.Visibility = Visibility.Collapsed;
            TxtGradingLoading.Visibility = Visibility.Visible;

            LoadGradingListAsync();
        }

        private async void LoadGradingListAsync()
        {
            if (_currentViewedAssignment == null) return;
            try
            {
                TxtGradingLoading.Visibility = Visibility.Visible;
                var submissions = await ApiService.GetAsync<List<SubmissionResponse>>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions");
                
                var list = new List<GradingItem>();
                if (submissions != null)
                {
                    foreach (var subResp in submissions)
                    {
                        var sub = subResp.Data;
                        string sId = sub.StudentId;
                        
                        var userResp = await ApiService.GetAsync<UserResponse>($"users/{sId}");
                        string name = userResp?.Data != null && !string.IsNullOrWhiteSpace(userResp.Data.FullName) ? userResp.Data.FullName : "Học viên ẩn danh";

                        list.Add(new GradingItem
                        {
                            SubmissionId = subResp.Id,
                            StudentId = sId,
                            FullName = name,
                            FileUrl = sub.FileUrl,
                            DisplayFileName = GetDisplayNameFromUrl(sub.FileUrl, "submission"),
                            SubmittedAt = sub.SubmittedAt.ToLocalTime(),
                            IsLate = sub.IsLate,
                            Score = sub.Score,
                            Comment = sub.Comment ?? ""
                        });
                    }
                }

                _gradingList.Clear();
                foreach (var item in list) _gradingList.Add(item);

                GradingItemsList.ItemsSource = null;
                GradingItemsList.ItemsSource = _gradingList;
                TxtGradingLoading.Visibility = Visibility.Collapsed;

                if (_gradingList.Count > 0 && GradingItemsList.SelectedIndex == -1)
                {
                    GradingItemsList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tải danh sách chấm bài: " + ex.Message, "Lỗi", DialogType.Error);
                TxtGradingLoading.Visibility = Visibility.Collapsed;
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
                    CustomDialog.Show("Điểm phải là số từ 0 đến 10!", "Lỗi nhập liệu", DialogType.Warning);
                    return;
                }
            }

            string comment = InputComment.Text;

            try
            {
                BtnSaveAndNext.IsEnabled = false;
                BtnSaveAndNext.Content = "Đang lưu...";

                var req = new
                {
                    Score = score,
                    Comment = comment
                };

                await ApiService.PutAsync($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions/{_currentGradingItem.StudentId}/grade", req);
                await NotificationService.SendNotificationAsync(_dbManager, _currentGradingItem.StudentId, "Ä ã có điểm", $"Bài tập '{_currentViewedAssignment.Title}' của bạn đã có điểm: {score}/10", "Homework", courseId: _course.Id);

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
                    CustomDialog.Show("Đã chấm xong bài cuối cùng trong danh sách!", "Hoàn tất", DialogType.Success);
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi khi lưu điểm: " + ex.Message, "Lỗi", DialogType.Error);
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
            MainScrollViewer.Effect = null;
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

            var confirmed = CustomDialog.Confirm("Bạn có chắc chắn muốn công khai điểm cho toàn bộ sinh viên?\nSinh viên sẽ nhận được thông báo và xem được điểm ngay lập tức.", "Công khai điểm", "Công khai", "Hủy", DialogType.Warning);
            if (confirmed)
            {
                try
                {
                    var btn = sender as Button;
                    if (btn != null) { btn.Content = "Đang xử lý..."; btn.IsEnabled = false; }

                    await ApiService.PutAsync($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/publish-grades", new { });

                    TxtInstructorGradingStatus.Text = "Trạng thái: Đã công khai";
                    TxtInstructorGradingStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                    CustomDialog.Show("Đã công khai điểm thành công!", "Thành công", DialogType.Success);

                    if (btn != null) { btn.Content = "Công khai điểm"; btn.IsEnabled = true; }
                }
                catch (Exception ex)
                {
                    CustomDialog.Show("Lỗi khi công khai điểm: " + ex.Message, "Lỗi", DialogType.Error);
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
                CustomDialog.Show("Vui lòng chọn hoặc kéo thả tệp bài làm của bạn vào ô nét đứt trước khi nộp!", "Chưa chọn tệp", DialogType.Warning);
                return;
            }

            BtnDetailSubmit.Content = "Đang xử lý...";
            BtnDetailSubmit.IsEnabled = false;

            try
            {
                var submissions = await ApiService.GetAsync<List<SubmissionResponse>>($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submissions");
                var mySubmissionResp = submissions?.FirstOrDefault(s => s.Id == CurrentUserId);

                if (mySubmissionResp != null && mySubmissionResp.Data != null)
                {
                    var oldSubmission = mySubmissionResp.Data;
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

                var req = new { FileUrl = uploadResult.SecureUrl.ToString(), Content = "" };
                await ApiService.PostAsync($"courses/{_course.Id}/assignments/{_currentViewedAssignment.Id}/submit", req);

                await NotificationService.SendNotificationAsync(_dbManager, _course.InstructorId, "Học sinh nộp bài", $"Có học sinh vừa nộp bài tập '{_currentViewedAssignment.Title}'.", "System", CurrentUserId, "Học sinh", courseId: _course.Id);

                DetailSubmissionStatus.Text = "Đã nộp bài";
                DetailSubmissionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                BtnDetailSubmit.Content = "Nộp lại tệp khác";

                SubmittedFileDetailsPanel.Visibility = Visibility.Visible;
                TxtSubmittedFileName.Text = "Tệp: " + Path.GetFileName(_selectedSubmissionFilePath);
                TxtSubmittedTime.Text = "Thời gian nộp: " + DateTime.Now.ToString("HH:mm - dd/MM/yyyy");

                _selectedSubmissionFilePath = "";
                TxtSelectedFile.Text = "Kéo thả tệp vào đây hoặc nhấn để chọn";

                string statusMsg = isLate ? "Đã cập nhật bài nộp (TRỄ HẠN) thành công!" : "Đã cập nhật bài nộp (ĐÚNG HẠN) thành công!";
                CustomDialog.Show(statusMsg, "Thành công", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi trong quá trình nộp bài: " + ex.Message, "Lỗi", DialogType.Error);
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
                int firstDashIdx = _course.Semester.IndexOf(" - ");
                string semPart = _course.Semester.Substring(0, firstDashIdx).Trim();
                string yearPart = _course.Semester.Substring(firstDashIdx + 3).Trim();

                SetComboBoxByContent(EditSemesterInput, semPart);
                
                string standardYearPart = yearPart.Replace(" ", "");
                foreach (var item in EditYearInput.Items)
                {
                    string standardItem = item.ToString().Replace(" ", "");
                    if (standardItem == standardYearPart)
                    {
                        EditYearInput.SelectedItem = item;
                        break;
                    }
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
                    CustomDialog.Show("Tiết học không hợp lệ!\n- Tiết bắt đầu phải nhỏ hơn tiết kết thúc.\n- Cùng thuộc 1 buổi (Sáng: 1-5, Chiều: 6-10).", "Lỗi nhập liệu", DialogType.Warning);
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
            if (mainWin != null)
            {
                mainWin.MainContentArea.Content = new MyClassesView(_dbManager, CurrentUserId);
                return;
            }

            var studentWin = Window.GetWindow(this) as StudentMainWindow;
            if (studentWin != null)
            {
                studentWin.StudentContentArea.Content = new MyClassesView(_dbManager, CurrentUserId);
            }
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
            SetupPendingRequestsListener();
        }

        private void CloseApprovalDrawer_Click(object sender, RoutedEventArgs e)
        {
            ApprovalDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private async void SetupPendingRequestsListener()
        {
            try
            {
                var regs = await ApiService.GetAsync<List<RegistrationResponse>>($"courses/{_course.Id}/students");
                var list = new List<PendingRequest>();

                if (regs != null)
                {
                    foreach (var regResp in regs)
                    {
                        var reg = regResp.Data;
                        if (reg.status?.ToLower() == "pending")
                        {
                            // Use joined info from backend response
                            string name = reg.fullName ?? "Học viên ẩn danh";
                            string email = reg.email ?? "";

                            list.Add(new PendingRequest
                            {
                                RegistrationId = regResp.Id,
                                UserId = reg.userId,
                                FullName = name,
                                Email = email,
                                RequestedAt = (reg.requestDate ?? DateTime.UtcNow).ToLocalTime()
                            });
                        }
                    }
                }

                _pendingRequests.Clear();
                foreach (var req in list) _pendingRequests.Add(req);

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
                CustomDialog.Show("Lỗi tải danh sách chờ duyệt: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }



        private async void BtnAcceptStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                try
                {
                    var req = _pendingRequests.FirstOrDefault(r => r.RegistrationId == regId);
                    
                    await ApiService.PutAsync($"courses/{_course.Id}/registrations/{regId}/approve", new { });
                    
                    if (req != null) await NotificationService.SendNotificationAsync(_dbManager, req.UserId, "Vào lớp thành công", $"Yêu cầu tham gia lớp '{_course.ClassName}' của bạn đã được chấp nhận.", "System", CurrentUserId, "Giáo viên", courseId: _course.Id);

                    _course.StudentCount++;
                    await _dbManager.UpdateCourseAsync(_course);

                    TxtStudentCount.Text = _course.StudentCount.ToString();
                    SetupPendingRequestsListener();
                }
                catch (Exception ex) { CustomDialog.Show("Lỗi duyệt: " + ex.Message, "Lỗi", DialogType.Error); }
            }
        }

        private async void BtnRejectStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                try
                {
                    var req = _pendingRequests.FirstOrDefault(r => r.RegistrationId == regId);
                    
                    await ApiService.PutAsync($"courses/{_course.Id}/registrations/{regId}/reject", new { });
                    
                    if (req != null) await NotificationService.SendNotificationAsync(_dbManager, req.UserId, "Từ chối vào lớp", $"Yêu cầu tham gia lớp '{_course.ClassName}' của bạn đã bị từ chối.", "System", CurrentUserId, "Giáo viên", courseId: _course.Id);

                    SetupPendingRequestsListener();
                }
                catch (Exception ex) { CustomDialog.Show("Lỗi từ chối: " + ex.Message, "Lỗi", DialogType.Error); }
            }
        }

        private async void BtnAcceptAll_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingRequests.Count == 0) return;

            BtnAcceptAll.Content = "Đang xử lý...";
            BtnAcceptAll.IsEnabled = false;

            try
            {
                foreach (var req in _pendingRequests)
                {
                    await ApiService.PutAsync($"courses/{_course.Id}/registrations/{req.RegistrationId}/approve", new { });
                    await NotificationService.SendNotificationAsync(_dbManager, req.UserId, "Vào lớp thành công", $"Yêu cầu tham gia lớp '{_course.ClassName}' của bạn đã được chấp nhận.", "System", CurrentUserId, "Giáo viên", courseId: _course.Id);
                }

                _course.StudentCount += _pendingRequests.Count;
                await _dbManager.UpdateCourseAsync(_course);

                TxtStudentCount.Text = _course.StudentCount.ToString();
                SetupPendingRequestsListener();

                CustomDialog.Show("Đã duyệt tất cả yêu cầu thành công!", "Thành công", DialogType.Success);
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi duyệt tất cả: " + ex.Message, "Lỗi", DialogType.Error);
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
            SetupEnrolledStudentsListener();
        }

        private void CloseManageStudentsDrawer_Click(object sender, RoutedEventArgs e)
        {
            ManageStudentsDrawer.Visibility = Visibility.Collapsed;
            MainScrollViewer.Effect = null;
        }

        private async void SetupEnrolledStudentsListener()
        {
            try
            {
                var regs = await ApiService.GetAsync<List<RegistrationResponse>>($"courses/{_course.Id}/students");
                var list = new List<EnrolledStudent>();

                if (regs != null)
                {
                    foreach (var regResp in regs)
                    {
                        var reg = regResp.Data;
                        if (reg.status?.ToLower() == "active" || reg.status?.ToLower() == "accepted")
                        {
                            var userResp = await ApiService.GetAsync<UserResponse>($"users/{reg.userId}");
                            string name = userResp?.Data != null && !string.IsNullOrWhiteSpace(userResp.Data.FullName) ? userResp.Data.FullName : (reg.fullName ?? "Học viên ẩn danh");
                            string email = userResp?.Data != null ? userResp.Data.Email : (reg.email ?? "");

                            list.Add(new EnrolledStudent
                            {
                                RegistrationId = regResp.Id,
                                UserId = reg.userId,
                                FullName = name,
                                Email = email,
                                ApprovedDate = (reg.requestDate ?? DateTime.UtcNow).ToLocalTime()
                            });
                        }
                    }
                }

                _enrolledStudents.Clear();
                foreach (var s in list) _enrolledStudents.Add(s);

                EnrolledStudentsList.ItemsSource = _enrolledStudents;
                TxtNoEnrolledStudents.Visibility = _enrolledStudents.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

                _course.StudentCount = _enrolledStudents.Count;
                TxtStudentCount.Text = _course.StudentCount.ToString();
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tải danh sách học viên: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }

        private async void BtnRemoveStudent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string regId)
            {
                var confirmed = CustomDialog.Confirm("Bạn có chắc chắn muốn loại bỏ sinh viên này khỏi lớp? Trạng thái sẽ được chuyển thành 'Từ chối'.", "Xác nhận loại bỏ", "Loại bỏ", "Hủy", DialogType.Warning);
                if (confirmed)
                {
                    btn.IsEnabled = false;

                    try
                    {
                        var removedStudent = _enrolledStudents.FirstOrDefault(s => s.RegistrationId == regId);

                        await ApiService.PutAsync($"courses/{_course.Id}/registrations/{regId}/reject", new { });

                        if (_course.StudentCount > 0)
                        {
                            _course.StudentCount--;
                        }

                        await _dbManager.UpdateCourseAsync(_course);

                        if (removedStudent != null)
                        {
                            await NotificationService.SendNotificationAsync(_dbManager, removedStudent.UserId, "Rời khỏi lớp", $"Bạn đã bị giáo viên loại khỏi lớp '{_course.ClassName}'.", "System", CurrentUserId, "Giáo viên", courseId: _course.Id);
                        }

                        TxtStudentCount.Text = _course.StudentCount.ToString();

                        SetupEnrolledStudentsListener();

                        CustomDialog.Show("Đã loại bỏ sinh viên và chặn quyền truy cập thành công!", "Thông báo", DialogType.Success);
                    }
                    catch (Exception ex)
                    {
                        CustomDialog.Show("Lỗi khi xử lý: " + ex.Message, "Lỗi", DialogType.Error);
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

        private void VideoDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    TxtSelectedVideoFile.Text = System.IO.Path.GetFileName(filePath);
                    TxtSelectedVideoFile.Tag = filePath;
                }
            }
        }

        private void DocDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    AddDocPathInput.Text = System.IO.Path.GetFileName(filePath);
                    AddDocPathInput.Tag = filePath;
                }
            }
        }


        private void LessonItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Lesson selectedLesson)
            {
                var mainWin = Window.GetWindow(this) as MainWindow;
                if (mainWin != null)
                {
                    mainWin.MainContentArea.Content = new StudentCourseView(_dbManager, _course, selectedLesson);
                    return;
                }

                var studentWin = Window.GetWindow(this) as StudentMainWindow;
                if (studentWin != null)
                {
                    studentWin.StudentContentArea.Content = new StudentCourseView(_dbManager, _course, selectedLesson);
                }
            }
        }

        private void BtnEditVideo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lessonId)
            {
                _editingLesson = _lessons.FirstOrDefault(l => l.Id == lessonId);
                if (_editingLesson == null) return;

                TxtContentFormTitle.Text = "Chỉnh sửa Video";
                InputVideoTitle.Text = _editingLesson.Title;
                InputVideoDesc.Text = _editingLesson.Description;
                TxtSelectedVideoFile.Text = "Video đã tải lên (Nhấn để thay đổi)";
                TxtSelectedVideoFile.Tag = null;

                BtnConfirmAddVideo.Content = "Lưu thay đổi";

                MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                AddVideoDrawer.Visibility = Visibility.Visible;
            }
        }

        private void BtnDeleteVideo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string lessonId)
            {
                _lessonToDelete = _lessons.FirstOrDefault(l => l.Id == lessonId);
                if (_lessonToDelete != null)
                {
                    MainScrollViewer.Effect = new BlurEffect { Radius = 10 };
                    DeleteContentOverlay.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
