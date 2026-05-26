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
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class StudentDashboardView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private System.Windows.Threading.DispatcherTimer _pollingTimer;
        private List<Notification> _dashboardNotifs = new List<Notification>();
        private List<Course> _enrolledCoursesForNotif = new List<Course>();

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
            // Read notifications will be handled by the backend API 
            // and we rely on IsRead property returned from API.

            try
            {
                var registrations = await ApiService.GetAsync<List<System.Text.Json.JsonElement>>("courses/my-registrations");
                List<Course> enrolledCourses = new List<Course>();

                foreach (var reg in registrations)
                {
                    if (TryGetProp(reg, "data", out var dataProp))
                    {
                        if (TryGetProp(dataProp, "CourseId", out var courseIdProp) && TryGetProp(dataProp, "Status", out var statusProp))
                        {
                            if (statusProp.GetString()?.ToLower() == "accepted")
                            {
                                string courseId = courseIdProp.GetString();
                                try
                                {
                                    var courseResp = await ApiService.GetAsync<CourseResponse>($"courses/{courseId}");
                                    if (courseResp != null && courseResp.Data != null)
                                    {
                                        var c = courseResp.Data;
                                        c.Id = courseResp.Id;
                                        if (c.IsActive) enrolledCourses.Add(c);
                                    }
                                }
                                catch { }
                            }
                        }
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
                            var instDoc = await ApiService.GetAsync<System.Text.Json.JsonElement>($"users/{c.InstructorId}");
                            if (TryGetProp(instDoc, "data", out var data))
                            {
                                if (TryGetProp(data, "FullName", out var fn)) instName = fn.GetString();
                            }
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
                            var instDoc = await ApiService.GetAsync<System.Text.Json.JsonElement>($"users/{c.InstructorId}");
                            if (TryGetProp(instDoc, "data", out var data))
                            {
                                if (TryGetProp(data, "FullName", out var fn)) instName = fn.GetString();
                            }
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
                    try
                    {
                        var assigns = await ApiService.GetAsync<List<AssignmentResponse>>($"courses/{c.Id}/assignments");
                        foreach (var asm in assigns)
                        {
                            bool isSubmitted = false;
                            try
                            {
                                var subs = await ApiService.GetAsync<List<System.Text.Json.JsonElement>>($"courses/{c.Id}/assignments/{asm.Id}/submissions");
                                if (subs != null && subs.Any())
                                {
                                    isSubmitted = true;
                                }
                            }
                            catch { }

                            if (asm.Data != null)
                            {
                                if (!isSubmitted)
                                {
                                    pendingAssignmentsCount++;
                                }
                            }
                        }
                    }
                    catch { }
                }

                TxtPendingAssignments.Text = pendingAssignmentsCount.ToString();

                // 2. Tính số lượng bài kiểm tra cần làm
                int pendingExamsCount = 0;
                try
                {
                    var myHistory = await ApiService.GetAsync<List<ExamSubmissionResponse>>("exams/my-history");
                    var studentSubmissions = myHistory?.Select(r => r.Data).ToList() ?? new List<ExamSubmission>();

                    foreach (var c in enrolledCourses)
                    {
                        try
                        {
                            var exams = await ApiService.GetAsync<List<ExamResponse>>($"exams/course/{c.Id}");
                            foreach (var exam in exams)
                            {
                                if (exam.Data.IsPublished && exam.Data.IsActive)
                                {
                                    int attemptsCount = studentSubmissions.Count(s => s.ExamId == exam.Id);

                                    bool canTake = false;
                                    if (!exam.Data.AllowMultipleAttempts)
                                    {
                                        if (attemptsCount == 0) canTake = true;
                                    }
                                    else
                                    {
                                        if (attemptsCount < exam.Data.MaxAttempts) canTake = true;
                                    }

                                    if (canTake) pendingExamsCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Lỗi tính bài kiểm tra cho lớp {c.Id}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi lấy lịch sử bài thi: {ex.Message}");
                }

                TxtAvgScore.Text = pendingExamsCount.ToString();
                _enrolledCoursesForNotif = enrolledCourses;

                _pollingTimer = new System.Windows.Threading.DispatcherTimer();
                _pollingTimer.Interval = TimeSpan.FromSeconds(15);
                _pollingTimer.Tick += async (s, args) => await FetchNotificationsAsync();
                _pollingTimer.Start();

                _ = FetchNotificationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load Student Dashboard: " + ex.Message);
            }
        }

        private async Task FetchNotificationsAsync()
        {
            try
            {
                var notifs = await e_learning_app.Class.ApiService.GetAsync<List<Notification>>("notifications");
                if (notifs != null)
                {
                    _dashboardNotifs = notifs;
                    RefreshNotificationsUI();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi fetch notifs: " + ex.Message);
            }
        }

        private void RefreshNotificationsUI()
        {
            var sortedDocs = _dashboardNotifs.OrderByDescending(n => n.CreatedAt).Take(5).ToList();
            var notifList = new ObservableCollection<NotifItem>();

            foreach (var n in sortedDocs)
            {
                Course course = null;
                if (!string.IsNullOrEmpty(n.CourseId))
                {
                    course = _enrolledCoursesForNotif.FirstOrDefault(c => c.Id == n.CourseId);
                }

                notifList.Add(new NotifItem
                {
                    NotifKey = n.Id,
                    TargetCourse = course,
                    Title = n.Title,
                    Content = n.Content,
                    NotifType = n.Type,
                    Time = n.TimeAgo,
                    IsUnread = !n.IsRead
                });
            }

            if (notifList.Count > 0)
            {
                NotifItemsControl.ItemsSource = notifList;
            }
            else
            {
                var emptyList = new ObservableCollection<NotifItem>();
                emptyList.Add(new NotifItem { NotifKey = "empty", TargetCourse = null, Title = "Tuyệt vời! Bạn không có thông báo nào mới.", Time = "Ngay lúc này", IsUnread = false });
                NotifItemsControl.ItemsSource = emptyList;
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

        private void StudentDashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            _pollingTimer?.Stop();
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
                    try
                    {
                        await e_learning_app.Class.ApiService.PutAsync("notifications/read", new { NotificationIds = new List<string> { n.NotifKey } });
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
                        smw_exam.BtnQuiz_Click(null, null);
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
                mw.BtnSchedule_Click(null, null);
            }
        }


        private void BtnViewAllNotif_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is StudentMainWindow mw)
            {
                mw.BtnNotifications_Click(null, null);
            }
        }

        private static bool TryGetProp(System.Text.Json.JsonElement el, string name, out System.Text.Json.JsonElement val)
        {
            if (el.TryGetProperty(name, out val)) return true;
            if (char.IsUpper(name[0]))
            {
                string camel = char.ToLower(name[0]) + name.Substring(1);
                if (el.TryGetProperty(camel, out val)) return true;
            }
            else
            {
                string pascal = char.ToUpper(name[0]) + name.Substring(1);
                if (el.TryGetProperty(pascal, out val)) return true;
            }
            string upper = name.ToUpper();
            if (el.TryGetProperty(upper, out val)) return true;
            string lower = name.ToLower();
            if (el.TryGetProperty(lower, out val)) return true;
            return false;
        }
    }
}
