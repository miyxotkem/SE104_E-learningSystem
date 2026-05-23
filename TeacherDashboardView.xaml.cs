using e_learning_app;
using e_learning_app.Class;
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
    public partial class TeacherDashboardView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private System.Windows.Threading.DispatcherTimer _pollingTimer;
        private List<Notification> _dashboardNotifs = new List<Notification>();
        private List<Course> _myCoursesForNotif = new List<Course>();

        public class ScheduleItem
        {
            public Course TargetCourse { get; set; }
            public string TitleDisplay { get; set; }
            public string TimeSlot { get; set; }
            public string Room { get; set; }
            public string InfoDisplay { get; set; }
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

        public TeacherDashboardView(DatabaseManager dbManager)
        {
            InitializeComponent();
            _dbManager = dbManager;
            this.Unloaded += TeacherDashboardView_Unloaded;
        }

        private void TeacherDashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            _pollingTimer?.Stop();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTodayDate.Text = DateTime.Now.ToString("dddd, dd/MM/yyyy");
            var hour = DateTime.Now.Hour;
            string greeting = hour < 12 ? "buổi sáng" : hour < 18 ? "buổi chiều" : "buổi tối";

            var user = _dbManager.GetCurrentUser();
            string name = user != null ? user.FullName : "Thầy/Cô";
            TxtGreeting.Text = $"👋  Chào {greeting}, {name}!";

            LoadingOverlay.Visibility = Visibility.Visible;
            await LoadDashboardDataAsync();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async Task LoadDashboardDataAsync()
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            // Read notifications logic will be handled by the backend API 
            // and we will rely on IsRead property returned from API.

            try
            {
                var coursesResp = await e_learning_app.Class.ApiService.GetAsync<List<e_learning_app.Class.CourseResponse>>("courses");

                List<Course> myCourses = new List<Course>();
                int totalStudents = 0;

                if (coursesResp != null)
                {
                    foreach (var cr in coursesResp)
                    {
                        if (cr.Data == null) continue;
                        var c = cr.Data;
                        c.Id = cr.Id;
                        // Chỉ lấy course của instructor hiện tại và đang active
                        var currentUser2 = _dbManager.GetCurrentUser();
                        if (c.InstructorId != currentUser2?.Id) continue;
                        if (!c.IsActive) continue;
                        myCourses.Add(c);
                        totalStudents += c.StudentCount;
                    }
                }

                TxtTotalClasses.Text = myCourses.Count.ToString();
                TxtTotalStudents.Text = totalStudents.ToString();

                string todayStr = GetDayString(DateTime.Now);
                var todayCourses = myCourses.Where(c => c.DayOfWeek == todayStr).OrderBy(c => c.StartPeriod).ToList();

                TxtTodayInfo.Text = $"Hôm nay bạn có {todayCourses.Count} lớp lên lịch. Chúc một ngày tốt lành! 🚀";

                var scheduleList = new ObservableCollection<ScheduleItem>();
                if (todayCourses.Count > 0)
                {
                    foreach (var c in todayCourses)
                    {
                        bool isMorning = c.StartPeriod <= 5;
                        scheduleList.Add(new ScheduleItem
                        {
                            TargetCourse = c,
                            TitleDisplay = $"{c.Title} ({c.ClassName})",
                            TimeSlot = $"Tiết {c.StartPeriod} - {c.EndPeriod}",
                            Room = string.IsNullOrWhiteSpace(c.Category) ? "Online" : c.Category,
                            InfoDisplay = $"{c.StudentCount} học viên",
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
                        TitleDisplay = "Hôm nay không có lịch dạy.",
                        TimeSlot = "N/A",
                        Room = "N/A",
                        InfoDisplay = "0 học viên",
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

                    var futureCourses = myCourses.Where(c => c.DayOfWeek == dayStr).OrderBy(c => c.StartPeriod).ToList();

                    foreach (var c in futureCourses)
                    {
                        upcomingScheduleList.Add(new ScheduleItem
                        {
                            TargetCourse = c,
                            TitleDisplay = $"{c.Title} ({c.ClassName})",
                            TimeSlot = $"Tiết {c.StartPeriod} - {c.EndPeriod}",
                            Room = string.IsNullOrWhiteSpace(c.Category) ? "Online" : c.Category,
                            InfoDisplay = $"{c.StudentCount} học viên",
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
                        TitleDisplay = "Không có lịch dạy trong 3 ngày tới.",
                        TimeSlot = "N/A",
                        Room = "N/A",
                        InfoDisplay = "0 học viên",
                        Status = "Trống",
                        ThemeBrush = GetSolidColorBrush("#E2E8F0"),
                        StatusBg = GetSolidColorBrush("#F8FAFC"),
                        StatusFg = GetSolidColorBrush("#64748B")
                    });
                }
                UpcomingScheduleItemsControl.ItemsSource = upcomingScheduleList;

                int pendingGradingCount = 0;
                foreach (var c in myCourses)
                {
                    try
                    {
                        var assigns = await e_learning_app.Class.ApiService.GetAsync<List<AssignmentResponse>>($"courses/{c.Id}/assignments");
                        if (assigns != null)
                        {
                            foreach (var asm in assigns)
                            {
                                if (asm.Deadline != default && asm.Deadline.ToLocalTime() < DateTime.Now)
                                    pendingGradingCount++;
                            }
                        }
                    }
                    catch
                    {
                        // Nếu API lỗi thì bỏ qua, không dừng toàn bộ dashboard
                    }
                }

                TxtPendingGrading.Text = pendingGradingCount.ToString();
                _myCoursesForNotif = myCourses;

                _pollingTimer = new System.Windows.Threading.DispatcherTimer();
                _pollingTimer.Interval = TimeSpan.FromSeconds(15);
                _pollingTimer.Tick += async (s, args) => await FetchNotificationsAsync();
                _pollingTimer.Start();

                _ = FetchNotificationsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load Dashboard: " + ex.Message);
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
                    course = _myCoursesForNotif.FirstOrDefault(c => c.Id == n.CourseId);
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

        private void BtnNavigateToCourse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Course course)
            {
                if (Window.GetWindow(this) is MainWindow mw)
                {
                    mw.MainContentArea.Content = new CourseDetailView(_dbManager, course);
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
                    if (Window.GetWindow(this) is MainWindow mw_exam)
                    {
                        mw_exam.MainContentArea.Content = new ExamManagementView(_dbManager);
                    }
                    return;
                }

                if (n.TargetCourse != null && Window.GetWindow(this) is MainWindow mw)
                {
                    mw.MainContentArea.Content = new CourseDetailView(_dbManager, n.TargetCourse);
                }
            }
        }

        private void BtnCreateClass_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new CreateCoursesView(_dbManager));
        }

        private void BtnCreateExam_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NavigateTo(new CreateExamView(_dbManager));
        }

        private void BtnViewAllNotif_Click(object sender, RoutedEventArgs e) { }
    }
}