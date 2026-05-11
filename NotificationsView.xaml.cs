using e_learning_app;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Google.Cloud.Firestore;

namespace e_learning_app.Views
{
    public partial class NotificationsView : UserControl, INotifyPropertyChanged
    {
        private readonly DatabaseManager _dbManager;
        private ObservableCollection<NotifDisplayItem> _notifications = new();

        // ĐỔI KIỂU DỮ LIỆU: Dùng trực tiếp NotifDisplayItem để tránh lỗi thiếu thuộc tính
        private List<NotifDisplayItem> _all = new();

        private string _filterMode = "all";

        public ObservableCollection<NotifDisplayItem> Notifications
        {
            get => _notifications;
            set { _notifications = value; OnPropertyChanged(); }
        }

        public class NotifDisplayItem : INotifyPropertyChanged
        {
            public string Id { get; set; }
            public Course TargetCourse { get; set; } // Dùng để chuyển hướng
            public string Title { get; set; }
            public string Content { get; set; }
            public string Icon { get; set; }
            public string TimeDisplay { get; set; }
            public DateTime SortTime { get; set; } // Thêm trường này để sắp xếp chính xác

            private bool _isUnread;
            public bool IsUnread
            {
                get => _isUnread;
                set { _isUnread = value; OnPropertyChanged(); }
            }

            public string Type { get; set; }
            public string SenderId { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public NotificationsView(DatabaseManager db)
        {
            InitializeComponent();
            _dbManager = db;
            this.DataContext = this;
            //this.Loaded += UserControl_Loaded;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            // 1. Đồng bộ trạng thái đã đọc từ Firebase
            try
            {
                var readNotifsSnap = await _dbManager.GetDb.Collection("Users").Document(currentUser.Id)
                    .Collection("ReadNotifications").GetSnapshotAsync();

                NotificationService.ReadNotifKeys.Clear();
                foreach (var doc in readNotifsSnap.Documents)
                {
                    NotificationService.ReadNotifKeys.Add(doc.Id);
                }
            }
            catch { }

            _all.Clear();

            // 2. Lấy thông báo hệ thống trực tiếp vào NotifDisplayItem
            try
            {
                var snapshot = await _dbManager.GetDb.Collection("Notifications")
                    .OrderByDescending("CreatedAt")
                    .Limit(50)
                    .GetSnapshotAsync();

                foreach (var d in snapshot.Documents)
                {
                    DateTime created = d.ContainsField("CreatedAt") ? d.GetValue<DateTime>("CreatedAt").ToLocalTime() : DateTime.Now;
                    string notifId = d.Id;

                    _all.Add(new NotifDisplayItem
                    {
                        Id = notifId,
                        Title = d.ContainsField("Title") ? d.GetValue<string>("Title") : "Thông báo hệ thống",
                        Content = d.ContainsField("Content") ? d.GetValue<string>("Content") : "",
                        Icon = d.ContainsField("Icon") ? d.GetValue<string>("Icon") : "🔔",
                        Type = d.ContainsField("Type") ? d.GetValue<string>("Type") : "System",
                        SenderId = d.ContainsField("SenderId") ? d.GetValue<string>("SenderId") : "",
                        SortTime = created,
                        TimeDisplay = d.ContainsField("TimeAgo") ? d.GetValue<string>("TimeAgo") : created.ToString("dd/MM - HH:mm"),
                        IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId)
                    });
                }
            }
            catch { }

            // 3. QUÉT THÔNG BÁO DÀNH RIÊNG CHO GIÁO VIÊN
            try
            {
                var coursesSnap = await _dbManager.GetDb.Collection("Courses")
                    .WhereEqualTo("InstructorId", currentUser.Id)
                    .WhereEqualTo("IsActive", true)
                    .GetSnapshotAsync();

                foreach (var doc in coursesSnap.Documents)
                {
                    var c = doc.ConvertTo<Course>();
                    c.Id = doc.Id;
                    string className = string.IsNullOrEmpty(c.ClassName) ? "Lớp học" : c.ClassName;

                    // Yêu cầu tham gia lớp
                    var pendingSnap = await _dbManager.GetDb.Collection("courseRegistrations")
                        .WhereEqualTo("courseId", c.Id)
                        .WhereEqualTo("status", "pending")
                        .GetSnapshotAsync();

                    if (pendingSnap.Count > 0)
                    {
                        string notifId = $"req_{c.Id}";
                        _all.Add(new NotifDisplayItem
                        {
                            Id = notifId,
                            TargetCourse = c,
                            Title = "Yêu cầu tham gia lớp",
                            Content = $"Có {pendingSnap.Count} sinh viên đang chờ phê duyệt vào lớp {className}.",
                            Type = "System",
                            Icon = "👨‍🎓",
                            SortTime = DateTime.Now,
                            TimeDisplay = "Chờ duyệt",
                            IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId)
                        });
                    }

                    // Nhắc chấm điểm bài tập
                    var assigns = await _dbManager.GetDb.Collection("Courses").Document(c.Id).Collection("Assignments").GetSnapshotAsync();
                    foreach (var asm in assigns.Documents)
                    {
                        if (asm.ContainsField("Deadline"))
                        {
                            var deadlineUtc = asm.GetValue<DateTime>("Deadline");
                            var deadlineLocal = deadlineUtc.ToLocalTime();

                            if (deadlineLocal < DateTime.Now && (DateTime.Now - deadlineLocal).TotalDays <= 7)
                            {
                                string notifId = $"grade_{c.Id}_{asm.Id}";
                                string title = asm.ContainsField("Title") ? asm.GetValue<string>("Title") : "Bài tập";

                                _all.Add(new NotifDisplayItem
                                {
                                    Id = notifId,
                                    TargetCourse = c,
                                    Title = "Nhắc nhở chấm điểm",
                                    Content = $"Bài tập '{title}' của lớp {className} đã hết hạn. Vui lòng chấm điểm.",
                                    Type = "Assignment",
                                    Icon = "📝",
                                    SortTime = DateTime.Now,
                                    TimeDisplay = $"Hạn: {deadlineLocal:dd/MM}",
                                    IsUnread = !NotificationService.ReadNotifKeys.Contains(notifId)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi quét thông báo Giáo viên: " + ex.Message);
            }

            // Sắp xếp lại dựa trên SortTime
            _all = _all.OrderByDescending(n => n.SortTime).ToList();
            Refresh();
        }

        private async void BtnViewNotif_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NotifDisplayItem n)
            {
                if (n.IsUnread)
                {
                    n.IsUnread = false;
                    NotificationService.ReadNotifKeys.Add(n.Id);
                    BtnFilterUnread.Content = $"Chưa đọc ({_all.Count(n2 => n2.IsUnread)})";

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

                // Cập nhật hỗ trợ điều hướng cho cả 2 loại MainWindow
                if (n.TargetCourse != null)
                {
                    var window = Window.GetWindow(this);
                    if (window is StudentMainWindow smw)
                    {
                        smw.StudentContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                    }
                    else if (window is MainWindow mw)
                    {
                        mw.MainContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                    }
                }
            }
        }

        private void Refresh()
        {
            var currentUser = _dbManager.GetCurrentUser();
            var filtered = _filterMode switch
            {
                "unread" => _all.Where(n => n.IsUnread),
                "sent" => _all.Where(n => n.SenderId == currentUser?.Id),
                "system" => _all.Where(n => n.Type == "System"),
                _ => _all
            };

            var list = filtered.ToList();

            if (list.Count == 0)
            {
                list.Add(new NotifDisplayItem
                {
                    Id = "empty",
                    Title = "Chưa có thông báo nào",
                    Content = "Hộp thư của bạn đang trống. Hãy quay lại sau nhé!",
                    Icon = "🎐",
                    TimeDisplay = "Bây giờ",
                    IsUnread = false
                });
            }

            var displayList = new ObservableCollection<NotifDisplayItem>(list);
            NotifItemsControl.ItemsSource = displayList;

            BtnFilterAll.Content = $"Tất cả ({_all.Count})";
            BtnFilterUnread.Content = $"Chưa đọc ({_all.Count(n => n.IsUnread)})";
        }

        private void FilterTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _filterMode = rb.Tag.ToString();
                Refresh();
            }
        }

        private void BtnMarkAllRead_Click(object sender, RoutedEventArgs e) { }
        private void BtnCreateNotif_Click(object sender, RoutedEventArgs e)
        {
            CustomDialog.Show("Tính năng tạo thông báo đang được cập nhật.", "Thông báo", DialogType.Info);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}