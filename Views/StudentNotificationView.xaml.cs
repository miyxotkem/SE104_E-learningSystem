using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace e_learning_app.Views
{
    public static class NotificationService
    {
        // Cái giỏ dùng chung cho mọi màn hình
        public static HashSet<string> ReadNotifKeys = new HashSet<string>();
    }
    public partial class StudentNotificationView : UserControl, INotifyPropertyChanged
    {
        private readonly DatabaseManager _dbManager;

        // Danh sách gốc chứa TẤT CẢ thông báo để dùng khi lọc
        private List<NotifDisplayItem> _allNotifications = new List<NotifDisplayItem>();

        public class NotifDisplayItem : INotifyPropertyChanged
        {
            public string Id { get; set; }
            public Course TargetCourse { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string Icon { get; set; }
            public string TimeDisplay { get; set; }
            public string Type { get; set; }
            public DateTime SortTime { get; set; }

            private bool _isUnread = true;
            public bool IsUnread
            {
                get => _isUnread;
                set { _isUnread = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public StudentNotificationView(DatabaseManager db)
        {
            InitializeComponent();
            _dbManager = db;
            this.Loaded += UserControl_Loaded;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFullNotificationsAsync();
        }

        private async Task LoadFullNotificationsAsync()
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            // 0. Đồng bộ các thông báo đã đọc từ Firebase vào RAM
            try
            {
                var readNotifsSnap = await _dbManager.GetDb.Collection("Users").Document(currentUser.Id)
                    .Collection("ReadNotifications").GetSnapshotAsync();

                NotificationService.ReadNotifKeys.Clear(); // Xóa dữ liệu cũ của user trước (nếu có)
                foreach (var doc in readNotifsSnap.Documents)
                {
                    NotificationService.ReadNotifKeys.Add(doc.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi đồng bộ thông báo đã đọc: " + ex.Message);
            }

            try
            {
                _allNotifications.Clear();

                // 1. Lấy danh sách đăng ký khóa học (Sao chép logic chính xác từ Dashboard)
                var registrationsSnap = await _dbManager.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("userId", currentUser.Id)
                    .WhereEqualTo("status", "accepted")
                    .GetSnapshotAsync();

                List<Course> enrolledCourses = new List<Course>();
                foreach (var reg in registrationsSnap.Documents)
                {
                    string courseId = reg.GetValue<string>("courseId");
                    var courseSnap = await _dbManager.GetDb.Collection("Courses").Document(courseId).GetSnapshotAsync();

                    if (courseSnap.Exists)
                    {
                        var c = courseSnap.ConvertTo<Course>();
                        c.Id = courseSnap.Id;
                        if (c.IsActive) enrolledCourses.Add(c);
                    }
                }

                // 2. Lặp qua từng môn và lấy Assignments
                foreach (var c in enrolledCourses)
                {
                    var assigns = await _dbManager.GetDb.Collection("Courses").Document(c.Id).Collection("Assignments").GetSnapshotAsync();

                    foreach (var asm in assigns.Documents)
                    {
                        var title = asm.GetValue<string>("Title");
                        bool isSubmitted = false;

                        try
                        {
                            var subSnap = await _dbManager.GetDb.Collection("Courses").Document(c.Id)
                                .Collection("Assignments").Document(asm.Id)
                                .Collection("Submissions").WhereEqualTo("StudentId", currentUser.Id)
                                .GetSnapshotAsync();

                            isSubmitted = subSnap.Count > 0;
                        }
                        catch { }

                        // Check Deadline
                        if (asm.ContainsField("Deadline"))
                        {
                            var deadlineUtc = asm.GetValue<DateTime>("Deadline");
                            var deadlineLocal = deadlineUtc.ToLocalTime();

                            if (!isSubmitted)
                            {
                                if (deadlineLocal > DateTime.Now)
                                {
                                    double hoursLeft = (deadlineLocal - DateTime.Now).TotalHours;
                                    if (hoursLeft <= 48)
                                    {
                                        string notifId = $"deadline_{c.Id}_{asm.Id}";
                                        _allNotifications.Add(new NotifDisplayItem
                                        {
                                            Id = notifId,
                                            TargetCourse = c,
                                            Title = "Sắp hết hạn nộp bài!",
                                            Content = $"Bài tập '{title}' của lớp {c.ClassName} sắp hết hạn nộp. Hãy hoàn thành ngay!",
                                            Icon = "⏰",
                                            TimeDisplay = $"Hạn chót: {deadlineLocal:dd/MM - HH:mm}",
                                            SortTime = DateTime.Now, 
                                            Type = "Homework",
                                            IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId)
                                        });
                                    }
                                }
                                else
                                {
                                    string notifId = $"overdue_{c.Id}_{asm.Id}";
                                    _allNotifications.Add(new NotifDisplayItem
                                    {
                                        Id = $"overdue_{c.Id}_{asm.Id}",
                                        TargetCourse = c,
                                        Title = "Cảnh báo quá hạn!",
                                        Content = $"Bài tập '{title}' của lớp {c.ClassName} đã kết thúc hạn nộp.",
                                        Icon = "⚠️",
                                        TimeDisplay = $"Quá hạn lúc: {deadlineLocal:dd/MM - HH:mm}",
                                        SortTime = DateTime.Now,
                                        Type = "Warning",
                                        IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId)
                                    });
                                }
                            }
                        }

                        // Check CreatedAt
                        if (asm.ContainsField("CreatedAt"))
                        {
                            var createdUtc = asm.GetValue<DateTime>("CreatedAt");
                            var createdLocal = createdUtc.ToLocalTime();

                            // Lấy bài tập trong vòng 14 ngày qua
                            if (createdUtc >= DateTime.UtcNow.AddDays(-14))
                            {
                                // 1. TẠO BIẾN ID TRƯỚC
                                string notifId = $"new_{c.Id}_{asm.Id}";

                                _allNotifications.Add(new NotifDisplayItem
                                {
                                    Id = notifId, // 2. Gán vào Id
                                    TargetCourse = c,
                                    Title = $"Có bài tập mới: '{title}' ở lớp {c.ClassName}.",
                                    Content = "Nhấn để xem chi tiết bài tập.",
                                    Icon = "✨",
                                    TimeDisplay = $"Đăng ngày: {createdLocal:dd/MM}",
                                    SortTime = DateTime.Now,
                                    Type = "Homework",
                                    IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId) 
                                });
                            }
                        }
                    }
                }

                // Sắp xếp ưu tiên mới nhất lên đầu
                _allNotifications = _allNotifications.OrderByDescending(n => n.SortTime).ToList();

                // Trường hợp trống
                if (_allNotifications.Count == 0)
                {
                    _allNotifications.Add(new NotifDisplayItem
                    {
                        Id = "empty",
                        Title = "Tuyệt vời!",
                        Content = "Bạn không có bài tập nào quá hạn hoặc sắp hết hạn.",
                        Icon = "🎉",
                        TimeDisplay = "Ngay lúc này",
                        Type = "System",
                        IsUnread = false
                    });
                }

                // Hiển thị ra màn hình
                FilterAndDisplay("All");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load full notifications: " + ex.Message);
            }
        }

        private void FilterTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                FilterAndDisplay(rb.Tag.ToString());
            }
        }

        private void FilterAndDisplay(string filterType)
        {
            var filteredList = _allNotifications.AsEnumerable();

            switch (filterType)
            {
                case "Unread":
                    filteredList = _allNotifications.Where(n => n.IsUnread);
                    break;
                case "Homework":
                    filteredList = _allNotifications.Where(n => n.Type == "Homework");
                    break;
                case "Warning":
                    filteredList = _allNotifications.Where(n => n.Type == "Warning");
                    break;
                case "All":
                default:
                    filteredList = _allNotifications;
                    break;
            }

            // Dùng cách gán trực tiếp y hệt Dashboard để khắc phục lỗi không hiển thị
            var displayList = new ObservableCollection<NotifDisplayItem>(filteredList);
            NotifItemsControl.ItemsSource = displayList;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async void BtnNotifItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NotifDisplayItem n)
            {
                // 1. Đổi trạng thái thành "Đã đọc" và LƯU LẠI VÀO BỘ NHỚ RAM
                if (n.IsUnread)
                {
                    n.IsUnread = false;
                    NotificationService.ReadNotifKeys.Add(n.Id);

                    // 2. Lưu trạng thái "đã đọc" vĩnh viễn lên Firebase
                    try
                    {
                        var currentUser = _dbManager.GetCurrentUser();
                        if (currentUser != null)
                        {
                            await _dbManager.GetDb.Collection("Users").Document(currentUser.Id)
                                .Collection("ReadNotifications").Document(n.Id)
                                .SetAsync(new { ReadAt = DateTime.UtcNow });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi lưu trạng thái đã đọc: " + ex.Message);
                    }
                }

                // 2. Chuyển hướng sang trang Chi tiết khóa học
                if (n.TargetCourse != null)
                {
                    var currentWindow = Window.GetWindow(this);
                    if (currentWindow is StudentMainWindow smw)
                    {
                        smw.StudentContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                    }
                    else if (currentWindow is MainWindow mw)
                    {
                        mw.MainContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                    }
                }
            }
        }
    }
}