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

namespace e_learning_app.Views
{
    public partial class TeacherDashboardView : UserControl
    {
        private readonly DatabaseManager _dbManager;
        private Google.Cloud.Firestore.FirestoreChangeListener _userNotifListener;
        private Google.Cloud.Firestore.FirestoreChangeListener _systemNotifListener;
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
            _userNotifListener?.StopAsync();
            _systemNotifListener?.StopAsync();
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
                var coursesSnap = await _dbManager.GetDb.Collection("Courses")
                    .WhereEqualTo("InstructorId", currentUser.Id)
                    .WhereEqualTo("IsActive", true)
                    .GetSnapshotAsync();

                List<Course> myCourses = new List<Course>();
                int totalStudents = 0;

                foreach (var doc in coursesSnap.Documents)
                {
                    var c = doc.ConvertTo<Course>();
                    c.Id = doc.Id;
                    myCourses.Add(c);
                    totalStudents += c.StudentCount;
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
                    var assigns = await _dbManager.GetDb.Collection("Courses").Document(c.Id).Collection("Assignments").GetSnapshotAsync();
                    foreach (var asm in assigns.Documents)
                    {
                        if (asm.ContainsField("Deadline"))
                        {
                            var deadlineUtc = asm.GetValue<DateTime>("Deadline");
                            var deadlineLocal = deadlineUtc.ToLocalTime();
                            if (deadlineLocal < DateTime.Now) pendingGradingCount++;
                        }
                    }
                }

                TxtPendingGrading.Text = pendingGradingCount.ToString();
                _myCoursesForNotif = myCourses;

                _userNotifListener = _dbManager.GetDb.Collection("Notifications")
                    .WhereEqualTo("TargetId", currentUser.Id)
                    .Listen(snapshot =>
                    {
                        Application.Current.Dispatcher.Invoke(() => UpdateNotificationsList(snapshot));
                    });

                _systemNotifListener = _dbManager.GetDb.Collection("Notifications")
                    .WhereEqualTo("TargetId", "all")
                    .Listen(snapshot =>
                    {
                        Application.Current.Dispatcher.Invoke(() => UpdateNotificationsList(snapshot));
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load Dashboard: " + ex.Message);
            }
        }

        private void UpdateNotificationsList(Google.Cloud.Firestore.QuerySnapshot snapshot)
        {
            foreach (var change in snapshot.Changes)
            {
                var doc = change.Document;
                var notif = doc.ConvertTo<Notification>();
                notif.Id = doc.Id;
                
                if (change.ChangeType == Google.Cloud.Firestore.DocumentChange.Type.Added)
                {
                    if (!_dashboardNotifs.Any(n => n.Id == notif.Id))
                        _dashboardNotifs.Add(notif);
                }
                else if (change.ChangeType == Google.Cloud.Firestore.DocumentChange.Type.Modified)
                {
                    var existing = _dashboardNotifs.FirstOrDefault(n => n.Id == notif.Id);
                    if (existing != null)
                    {
                        _dashboardNotifs.Remove(existing);
                        _dashboardNotifs.Add(notif);
                    }
                }
                else if (change.ChangeType == Google.Cloud.Firestore.DocumentChange.Type.Removed)
                {
                    var existing = _dashboardNotifs.FirstOrDefault(n => n.Id == notif.Id);
                    if (existing != null)
                        _dashboardNotifs.Remove(existing);
                }
            }

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
                    IsUnread = !NotificationService.ReadNotifKeys.Contains(n.Id)
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