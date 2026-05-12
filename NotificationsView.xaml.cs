using e_learning_app;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Google.Cloud.Firestore;

namespace e_learning_app.Views
{
    public partial class NotificationsView : UserControl, INotifyPropertyChanged
    {
        private readonly DatabaseManager _dbManager;
        private ObservableCollection<Notification> _notifications = new();
        private List<Notification> _all = new();
        private string _filterMode = "all";
        
        private FirestoreChangeListener _userNotifListener;
        private FirestoreChangeListener _systemNotifListener;

        public ObservableCollection<Notification> Notifications
        {
            get => _notifications;
            set { _notifications = value; OnPropertyChanged(); }
        }

        public NotificationsView(DatabaseManager db)
        {
            InitializeComponent();
            _dbManager = db;
            this.DataContext = this;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var currentUser = _dbManager.GetCurrentUser();
            if (currentUser == null) return;

            // 1. Đồng bộ trạng thái đã đọc vào RAM
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

            // 2. Lắng nghe thông báo cá nhân (Real-time)
            _userNotifListener = _dbManager.GetDb.Collection("Notifications")
                .WhereEqualTo("TargetId", currentUser.Id)
                .Listen(snapshot =>
                {
                    UpdateNotificationsFromSnapshot(snapshot);
                });

            // 3. Lắng nghe thông báo hệ thống toàn cầu (Real-time)
            _systemNotifListener = _dbManager.GetDb.Collection("Notifications")
                .WhereEqualTo("TargetId", "all")
                .Listen(snapshot =>
                {
                    UpdateNotificationsFromSnapshot(snapshot);
                });
        }

        private void UpdateNotificationsFromSnapshot(QuerySnapshot snapshot)
        {
            // Cập nhật hoặc thêm mới vào danh sách gốc (_all)
            foreach (var change in snapshot.Changes)
            {
                var doc = change.Document;
                var notif = doc.ConvertTo<Notification>();
                notif.Id = doc.Id;
                
                // Cập nhật trạng thái đã đọc từ RAM
                notif.IsRead = NotificationService.ReadNotifKeys.Contains(notif.Id);

                if (change.ChangeType == DocumentChange.Type.Added)
                {
                    // Nếu chưa có thì thêm vào
                    if (!_all.Any(n => n.Id == notif.Id))
                        _all.Add(notif);
                }
                else if (change.ChangeType == DocumentChange.Type.Modified)
                {
                    var existing = _all.FirstOrDefault(n => n.Id == notif.Id);
                    if (existing != null)
                    {
                        _all.Remove(existing);
                        _all.Add(notif);
                    }
                }
                else if (change.ChangeType == DocumentChange.Type.Removed)
                {
                    var existing = _all.FirstOrDefault(n => n.Id == notif.Id);
                    if (existing != null) _all.Remove(existing);
                }
            }

            // Sắp xếp lại theo thời gian
            _all = _all.OrderByDescending(n => n.CreatedAt).ToList();
            
            // Cập nhật giao diện
            Application.Current.Dispatcher.Invoke(() => Refresh());
        }

        private void Refresh()
        {
            var currentUser = _dbManager.GetCurrentUser();
            var filtered = _filterMode switch
            {
                "unread" => _all.Where(n => !n.IsRead),
                "sent" => _all.Where(n => n.SenderId == currentUser?.Id),
                "system" => _all.Where(n => n.Type == "System"),
                _ => _all
            };

            var list = filtered.ToList();

            if (list.Count == 0)
            {
                // Thêm một item "trống" báo hiệu không có thông báo
                var emptyNotif = new Notification
                {
                    Id = "empty",
                    Title = "Chưa có thông báo nào",
                    Content = "Hộp thư của bạn đang trống. Hãy quay lại sau nhé!",
                    Type = "System",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = true
                };
                list.Add(emptyNotif);
            }

            Notifications = new ObservableCollection<Notification>(list);

            BtnFilterAll.Content = $"Tất cả ({_all.Count})";
            BtnFilterUnread.Content = $"Chưa đọc ({_all.Count(n => !n.IsRead)})";
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
                    Refresh(); // Cập nhật số lượng chưa đọc

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

                // 2. Tạm thời vô hiệu hóa chuyển hướng chi tiết môn học vì cần thêm TargetCourseId vào DB
                // Khi nào rảnh, bạn thêm trường CourseId vào Class/Notification.cs là sẽ chuyển hướng được.
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

        private void BtnMarkAllRead_Click(object sender, RoutedEventArgs e) 
        {
            // Tính năng có thể mở rộng sau
        }

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