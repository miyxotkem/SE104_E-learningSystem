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

namespace e_learning_app.Views
{
    public partial class StudentDashboardView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        //private static HashSet<string> _readNotifKeys = new HashSet<string>();

        public class ScheduleItem
        {
            public Course TargetCourse { get; set; }
            public string TitleDisplay { get; set; }
            public string TimeSlot { get; set; }
            public string Room { get; set; }
            public string InstructorName { get; set; }
            public string Status { get; set; }
            public Brush ThemeBrush { get; set; }
            public Brush StatusBg { get; set; }
            public Brush StatusFg { get; set; }
        }

        public class NotifItem : INotifyPropertyChanged
        {
            public string NotifKey { get; set; }
            public Course TargetCourse { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string NotifType { get; set; }
            public string Time { get; set; }

            private bool _isUnread;
            public bool IsUnread
            {
                get => _isUnread;
                set
                {
                    if (_isUnread != value)
                    {
                        _isUnread = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(UnreadDotColor));
                        OnPropertyChanged(nameof(BackgroundBrush));
                        OnPropertyChanged(nameof(TitleWeight));
                        OnPropertyChanged(nameof(TitleBrush));
                        OnPropertyChanged(nameof(TimeBrush));
                    }
                }
            }

            public Brush UnreadDotColor => IsUnread ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)) : Brushes.Transparent;
            public Brush BackgroundBrush => IsUnread ? new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xFF)) : Brushes.Transparent;
            public FontWeight TitleWeight => IsUnread ? FontWeights.Bold : FontWeights.SemiBold;
            public Brush TitleBrush => IsUnread ? new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) : new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            public Brush TimeBrush => IsUnread ? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)) : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public StudentDashboardView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTodayDate.Text = DateTime.Now.ToString("dddd, dd/MM/yyyy");
            var hour = DateTime.Now.Hour;
            string greeting = hour < 12 ? "Chào buổi sáng" : hour < 18 ? "Chào buổi chiều" : "Chào buổi tối";

            var user = _dbManager.GetCurrentUser();
            string name = user != null ? user.FullName : "Sinh viên";
            TxtGreeting.Text = $"{greeting}, {name}! 👋";

            LoadingOverlay.Visibility = Visibility.Visible;
            await LoadDashboardDataAsync();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async Task LoadDashboardDataAsync()
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            // Đồng bộ trạng thái đã đọc từ Firebase
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
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi đồng bộ ReadNotifications: " + ex.Message);
            }

            try
            {
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

                TxtTotalCourses.Text = enrolledCourses.Count.ToString();

                string todayStr = GetDayString(DateTime.Now);
                var todayCourses = enrolledCourses.Where(c => c.DayOfWeek == todayStr).OrderBy(c => c.StartPeriod).ToList();

                TxtTodayInfo.Text = $"Hôm nay bạn có {todayCourses.Count} môn học. Cố lên nhé! 🚀";

                var scheduleList = new ObservableCollection<ScheduleItem>();
                if (todayCourses.Count > 0)
                {
                    foreach (var c in todayCourses)
                    {
                        bool isMorning = c.StartPeriod <= 5;
                        string instName = "Giảng viên phụ trách";
                        try
                        {
                            var instDoc = await _dbManager.GetDb.Collection("Users").Document(c.InstructorId).GetSnapshotAsync();
                            if (instDoc.Exists) instName = instDoc.GetValue<string>("FullName");
                        }
                        catch { }

                        scheduleList.Add(new ScheduleItem
                        {
                            TargetCourse = c,
                            TitleDisplay = $"{c.Title} ({c.ClassName})",
                            TimeSlot = $"Tiết {c.StartPeriod} - {c.EndPeriod}",
                            Room = string.IsNullOrWhiteSpace(c.Category) ? "Phòng học chung" : c.Category,
                            InstructorName = instName,
                            Status = isMorning ? "Ca Sáng" : "Ca Chiều",
                            ThemeBrush = GetSolidColorBrush(c.AccentColor, "#3B82F6"),
                            StatusBg = GetSolidColorBrush(isMorning ? "#DCFCE7" : "#FEF3C7"),
                            StatusFg = GetSolidColorBrush(isMorning ? "#16A34A" : "#D97706")
                        });
                    }
                }
                else
                {
                    scheduleList.Add(new ScheduleItem
                    {
                        TargetCourse = null,
                        TitleDisplay = "Hôm nay bạn không có lịch học trên trường.",
                        TimeSlot = "N/A",
                        Room = "N/A",
                        InstructorName = "Tự học",
                        Status = "Nghỉ",
                        ThemeBrush = GetSolidColorBrush("#E2E8F0"),
                        StatusBg = GetSolidColorBrush("#F8FAFC"),
                        StatusFg = GetSolidColorBrush("#64748B")
                    });
                }
                ScheduleItemsControl.ItemsSource = scheduleList;

                var upcomingScheduleList = new ObservableCollection<ScheduleItem>();
                for (int i = 1; i <= 3; i++)
                {
                    DateTime futureDate = DateTime.Now.AddDays(i);
                    string dayStr = GetDayString(futureDate);
                    string shortDate = futureDate.ToString("dd/MM");

                    var futureCourses = enrolledCourses.Where(c => c.DayOfWeek == dayStr).OrderBy(c => c.StartPeriod).ToList();

                    foreach (var c in futureCourses)
                    {
                        string instName = "Giảng viên";
                        try
                        {
                            var instDoc = await _dbManager.GetDb.Collection("Users").Document(c.InstructorId).GetSnapshotAsync();
                            if (instDoc.Exists) instName = instDoc.GetValue<string>("FullName");
                        }
                        catch { }

                        upcomingScheduleList.Add(new ScheduleItem
                        {
                            TargetCourse = c,
                            TitleDisplay = $"{c.Title} ({c.ClassName})",
                            TimeSlot = $"Tiết {c.StartPeriod} - {c.EndPeriod}",
                            Room = string.IsNullOrWhiteSpace(c.Category) ? "Phòng chung" : c.Category,
                            InstructorName = instName,
                            Status = $"{dayStr} ({shortDate})",
                            ThemeBrush = GetSolidColorBrush(c.AccentColor, "#3B82F6"),
                            StatusBg = GetSolidColorBrush("#F1F5F9"),
                            StatusFg = GetSolidColorBrush("#475569")
                        });
                    }
                }

                if (upcomingScheduleList.Count == 0)
                {
                    upcomingScheduleList.Add(new ScheduleItem
                    {
                        TargetCourse = null,
                        TitleDisplay = "Không có lịch học trên trường trong 3 ngày tới.",
                        TimeSlot = "N/A",
                        Room = "N/A",
                        InstructorName = "Tự học",
                        Status = "Trống",
                        ThemeBrush = GetSolidColorBrush("#E2E8F0"),
                        StatusBg = GetSolidColorBrush("#F8FAFC"),
                        StatusFg = GetSolidColorBrush("#64748B")
                    });
                }
                UpcomingScheduleItemsControl.ItemsSource = upcomingScheduleList;

                var notifList = new ObservableCollection<NotifItem>();
                int pendingAssignmentsCount = 0;

                // 1. Tính pendingAssignmentsCount
                foreach (var c in enrolledCourses)
                {
                    var assigns = await _dbManager.GetDb.Collection("Courses").Document(c.Id).Collection("Assignments").GetSnapshotAsync();
                    foreach (var asm in assigns.Documents)
                    {
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

                        if (asm.ContainsField("Deadline"))
                        {
                            var deadlineLocal = asm.GetValue<DateTime>("Deadline").ToLocalTime();
                            if (!isSubmitted && deadlineLocal > DateTime.Now)
                            {
                                pendingAssignmentsCount++;
                            }
                        }
                    }
                }

                TxtPendingAssignments.Text = pendingAssignmentsCount.ToString();

                // 2. Lấy danh sách Notifications đồng bộ
                var notifsSnap = await _dbManager.GetDb.Collection("Notifications")
                    .WhereEqualTo("TargetId", currentUser.Id)
                    .GetSnapshotAsync();

                var sortedDocs = notifsSnap.Documents
                    .Select(d => { var n = d.ConvertTo<Notification>(); n.Id = d.Id; return n; })
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToList();

                foreach (var n in sortedDocs)
                {
                    Course course = null;
                    if (!string.IsNullOrEmpty(n.CourseId))
                    {
                        course = enrolledCourses.FirstOrDefault(c => c.Id == n.CourseId);
                    }

                    notifList.Add(new NotifItem
                    {
                        NotifKey = n.Id,
                        TargetCourse = course,
                        Title = n.Title,
                        Content = n.Content,
                        NotifType = n.Type,
                        Time = n.TimeAgo,
                        IsUnread = !NotificationService.ReadNotifKeys.Contains(n.Id)
                    });
                }

                TxtPendingAssignments.Text = pendingAssignmentsCount.ToString();

                if (notifList.Count == 0)
                {
                    notifList.Add(new NotifItem { NotifKey = "empty", TargetCourse = null, Title = "Tuyệt vời! Bạn không có thông báo nào mới.", Time = "Ngay lúc này", IsUnread = false });
                }

                NotifItemsControl.ItemsSource = notifList;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load Student Dashboard: " + ex.Message);
            }
        }

        private string GetDayString(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => "Thứ 2",
                DayOfWeek.Tuesday => "Thứ 3",
                DayOfWeek.Wednesday => "Thứ 4",
                DayOfWeek.Thursday => "Thứ 5",
                DayOfWeek.Friday => "Thứ 6",
                DayOfWeek.Saturday => "Thứ 7",
                DayOfWeek.Sunday => "Chủ nhật",
                _ => "Thứ 2"
            };
        }

        private SolidColorBrush GetSolidColorBrush(string hexCode, string defaultHex = "#3B82F6")
        {
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hexCode); }
            catch { return (SolidColorBrush)new BrushConverter().ConvertFromString(defaultHex); }
        }

        private void BtnNavigateToCourse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Course course)
            {
                if (Window.GetWindow(this) is StudentMainWindow mw)
                {
                    mw.StudentContentArea.Content = new CourseDetailView(_dbManager, course);
                }
            }
        }

        private async void BtnNotifItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NotifItem n)
            {
                if (n.IsUnread)
                {
                    n.IsUnread = false;
                    NotificationService.ReadNotifKeys.Add(n.NotifKey);

                    // Lưu trạng thái đã đọc lên Firebase
                    try
                    {
                        var currentUser = _dbManager.GetCurrentUser();
                        if (currentUser != null)
                        {
                            await _dbManager.GetDb.Collection("Users").Document(currentUser.Id)
                                .Collection("ReadNotifications").Document(n.NotifKey)
                                .SetAsync(new { ReadAt = DateTime.UtcNow });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi lưu trạng thái đã đọc: " + ex.Message);
                    }
                }

                if (n.NotifType == "Exam")
                {
                    if (Window.GetWindow(this) is StudentMainWindow smw_exam)
                    {
                        smw_exam.StudentContentArea.Content = new StudentQuizView(_dbManager);
                    }
                    return;
                }

                if (n.TargetCourse != null && Window.GetWindow(this) is StudentMainWindow mw)
                {
                    mw.StudentContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                }
            }
        }

        private void BtnQuickCourses_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Window.GetWindow(this) is StudentMainWindow mw)
            {
                mw.StudentContentArea.Content = new TeachingScheduleView(_dbManager);

            }
        }


        private void BtnViewAllNotif_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is StudentMainWindow mw)
            {
                mw.BtnNotifications_Click(null, null);
            }
        }
    }
}