using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace e_learning_app
{
    public static class NotificationService
    {
        // Vẫn giữ giỏ RAM dùng tạm cho phiên làm việc hiện tại
        public static HashSet<string> ReadNotifKeys = new HashSet<string>();

        /// <summary>
        /// Gửi thông báo đến một người dùng cụ thể
        /// </summary>
        public static async Task SendNotificationAsync(DatabaseManager db, string targetUserId, string title, string content, string type, string senderId = "System", string senderName = "Hệ thống")
        {
            try
            {
                var notif = new Notification
                {
                    Title = title,
                    Content = content,
                    TargetId = targetUserId,
                    Type = type,
                    SenderId = senderId,
                    SenderName = senderName,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                await db.GetDb.Collection("Notifications").AddAsync(notif);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi thông báo: " + ex.Message);
            }
        }

        /// <summary>
        /// Gửi thông báo cho toàn bộ sinh viên trong một lớp (Dùng khi đăng bài tập mới)
        /// </summary>
        public static async Task SendToClassAsync(DatabaseManager db, string courseId, string title, string content, string type, string senderId = "", string senderName = "")
        {
            try
            {
                // Lấy danh sách học sinh đã duyệt vào lớp
                var snapshot = await db.GetDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", courseId)
                    .WhereEqualTo("status", "accepted")
                    .GetSnapshotAsync();

                var batch = db.GetDb.StartBatch();
                var notifRef = db.GetDb.Collection("Notifications");

                foreach (var doc in snapshot.Documents)
                {
                    string studentId = doc.GetValue<string>("userId");
                    
                    var notif = new Notification
                    {
                        Title = title,
                        Content = content,
                        TargetId = studentId, // Gửi đích danh cho từng học sinh
                        Type = type,
                        SenderId = senderId,
                        SenderName = senderName,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    batch.Create(notifRef.Document(), notif);
                }

                await batch.CommitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi thông báo lớp: " + ex.Message);
            }
        }
    }
}
