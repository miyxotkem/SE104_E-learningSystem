using e_learning_app;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading.Tasks;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class NotificationsView : UserControl, INotifyPropertyChanged
    {
        private readonly DatabaseManager _dbManager;
        private ObservableCollection<Notification> _notifications = new();
        private List<Notification> _all = new();
        private string _filterMode = "all";
        



        public ObservableCollection<Notification> Notifications
        {
            get => _notifications;
            set { _notifications = value; OnPropertyChanged(); }
        }

        private DispatcherTimer _pollingTimer;

        public NotificationsView(DatabaseManager db)
        {
            InitializeComponent();
            _dbManager = db;
            this.DataContext = this;
            this.Unloaded += NotificationsView_Unloaded;
        }

        private void NotificationsView_Unloaded(object sender, RoutedEventArgs e)
        {
            _pollingTimer?.Stop();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(15);
            _pollingTimer.Tick += async (s, args) => await FetchNotificationsAsync();
            _pollingTimer.Start();

            _ = FetchNotificationsAsync();
        }

        private async Task FetchNotificationsAsync()
        {
            try
            {
                var notifs = await ApiService.GetAsync<List<Notification>>("notifications");
                if (notifs != null)
                {
                    _all = notifs;
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching notifications: " + ex.Message);
            }
        }

        private void Refresh()
        {
            var currentUser = _dbManager.GetCurrentUser();
            var filtered = _filterMode switch
            {
                "unread" => _all.Where(n => !n.IsRead),
                "sent" => _all.Where(n => n.SenderId == currentUser?.Id),
                _ => _all
            };

            var list = filtered.ToList();

            if (list.Count == 0)
            {
                // Thêm một item "trống" báo hiệu không có thông báo
                var emptyNotif = new Notification
                {
                    Id = "empty",
                    Title = "Chua có thông báo nào",
                    Content = "Hộp thu của bạn dang trống. Hãy quay lại sau nhé!",
                    Type = "System",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = true
                };
                list.Add(emptyNotif);
            }

            Notifications = new ObservableCollection<Notification>(list);

            BtnFilterAll.Content = $"Tất cả ({_all.Count})";
            BtnFilterUnread.Content = $"Chua đọc ({_all.Count(n => !n.IsRead)})";
        }

        private async void BtnViewNotif_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Notification n)
            {
                if (n.Id == "empty") return;

                // 1. Đánh dấu đã đọc
                if (!n.IsRead)
                {
                    n.IsRead = true;
                    NotificationService.ReadNotifKeys.Add(n.Id);
                    Refresh(); // Cập nhật số lượng chua đọc

                    try
                    {
                        var currentUser = _dbManager.GetCurrentUser();
                        if (currentUser != null)
                        {
                            await ApiService.PutAsync("notifications/read", new { NotificationIds = new List<string> { n.Id } });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi luu trạng thái đã đọc: " + ex.Message);
                    }
                }

                // 2. Chuyển hướng chi tiết môn học
                if (!string.IsNullOrEmpty(n.CourseId))
                {
                    try
                    {
                        var courseResponse = await ApiService.GetAsync<CourseResponse>($"courses/{n.CourseId}");
                        if (courseResponse != null && courseResponse.Data != null)
                        {
                            var targetCourse = courseResponse.Data;
                            targetCourse.Id = courseResponse.Id;

                            var currentUser = _dbManager.GetCurrentUser();
                            if (currentUser != null && targetCourse.InstructorId != currentUser.Id && currentUser.Role != "Admin")
                            {
                                var regs = await ApiService.GetAsync<List<RegistrationResponse>>("courses/my-registrations");
                                bool isAccepted = regs?.Any(r => r.Data.courseId == targetCourse.Id && r.Data.status?.ToLower() == "accepted") ?? false;
                                
                                if (!isAccepted)
                                {
                                    CustomDialog.Show("Bạn không có quyền truy cập lớp học này (Đã rời lớp hoặc bị từ chối).", "Cảnh báo", DialogType.Warning);
                                    return;
                                }
                            }

                            var window = Window.GetWindow(this);
                            if (window == null) return;
                            
                            if (n.Type == "Exam")
                            {
                                if (window is StudentMainWindow smw)
                                {
                                    smw.StudentContentArea.Content = new e_learning_app.Views.StudentQuizView(_dbManager);
                                }
                                else if (window is MainWindow mw)
                                {
                                    mw.MainContentArea.Content = new ExamManagementView(_dbManager);
                                }
                                return;
                            }

                            if (window is StudentMainWindow smw2)
                            {
                                smw2.StudentContentArea.Content = new CourseDetailView(_dbManager, targetCourse);
                            }
                            else if (window is MainWindow mw2)
                            {
                                mw2.MainContentArea.Content = new CourseDetailView(_dbManager, targetCourse);
                            }
                        }
                        else
                        {
                            CustomDialog.Show("Lớp học này không còn tồn tại.", "Thông báo", DialogType.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Lỗi chuyển hướng: " + ex.Message);
                    }
                }
            }
        }

        private void FilterTab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _filterMode = rb.Tag.ToString();
                Refresh();
            }
        }

        private async void BtnMarkAllRead_Click(object sender, RoutedEventArgs e) 
        {
            var unreadIds = _all.Where(n => !n.IsRead && n.Id != "empty").Select(n => n.Id).ToList();
            if (unreadIds.Count == 0) return;

            foreach (var n in _all)
            {
                if (unreadIds.Contains(n.Id))
                {
                    n.IsRead = true;
                    NotificationService.ReadNotifKeys.Add(n.Id);
                }
            }
            Refresh();

            try
            {
                var currentUser = _dbManager.GetCurrentUser();
                if (currentUser != null)
                {
                    await ApiService.PutAsync("notifications/read", new { NotificationIds = unreadIds });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi đánh dấu đọc tất cả: " + ex.Message);
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
