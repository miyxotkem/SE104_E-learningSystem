using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace e_learning_app
{
    public static class NotificationService
    {
        // Vẫn giữ giỏ RAM dùng tạm cho phiên làm việc hiện tại
        public static HashSet<string> ReadNotifKeys = new HashSet<string>();

        private class SendNotifPayload
        {
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public string CourseId { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string SenderId { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
        }

        private class SendToClassPayload
        {
            public string CourseId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string SenderId { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Gửi thông báo đến một người dùng cụ thể (qua Backend API)
        /// </summary>
        public static async Task SendNotificationAsync(DatabaseManager db, string targetUserId, string title, string content, string type, string senderId = "System", string senderName = "Hệ thống", string courseId = "")
        {
            try
            {
                var payload = new SendNotifPayload
                {
                    Title = title,
                    Content = content,
                    TargetId = targetUserId,
                    CourseId = courseId,
                    Type = type,
                    SenderId = senderId,
                    SenderName = senderName
                };
                await e_learning_app.Class.ApiService.PostAsync("notifications/send", payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi thông báo: " + ex.Message);
            }
        }

        /// <summary>
        /// Gửi thông báo cho toàn bộ sinh viên trong một lớp (qua Backend API)
        /// </summary>
        public static async Task SendToClassAsync(DatabaseManager db, string courseId, string title, string content, string type, string senderId = "", string senderName = "")
        {
            try
            {
                var payload = new SendToClassPayload
                {
                    CourseId = courseId,
                    Title = title,
                    Content = content,
                    Type = type,
                    SenderId = senderId,
                    SenderName = senderName
                };
                await e_learning_app.Class.ApiService.PostAsync("notifications/send-to-class", payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi thông báo lớp: " + ex.Message);
            }
        }
    }
}
