using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI_E_learning.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public NotificationsController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException("User not found in token");
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            string uid = GetCurrentUserId();

            // Lấy danh sách khóa học đang tồn tại để ẩn thông báo của các khóa học đã xóa
            var coursesSnap = await _firestoreDb.Collection("Courses").GetSnapshotAsync();
            var activeCourseIds = coursesSnap.Documents.Select(d => d.Id).ToHashSet();

            // Lấy thông báo cá nhân
            var userNotifsSnap = await _firestoreDb.Collection("Notifications")
                .WhereEqualTo("TargetId", uid)
                .GetSnapshotAsync();

            // Lấy thông báo hệ thống (all)
            var systemNotifsSnap = await _firestoreDb.Collection("Notifications")
                .WhereEqualTo("TargetId", "all")
                .GetSnapshotAsync();

            // Lấy danh sách đã đọc
            var readSnap = await _firestoreDb.Collection("Users").Document(uid)
                .Collection("ReadNotifications").GetSnapshotAsync();
            
            var readIds = readSnap.Documents.Select(d => d.Id).ToHashSet();

            var notifications = new List<object>();

            foreach (var doc in userNotifsSnap.Documents.Concat(systemNotifsSnap.Documents))
            {
                var dict = doc.ToDictionary();
                string courseId = dict.ContainsKey("CourseId") ? dict["CourseId"].ToString() : "";
                
                // Nếu thông báo liên kết với một lớp học cụ thể nhưng lớp đó đã bị xóa, bỏ qua
                if (!string.IsNullOrEmpty(courseId) && !activeCourseIds.Contains(courseId))
                {
                    continue;
                }

                bool isRead = dict.ContainsKey("IsRead") && Convert.ToBoolean(dict["IsRead"]);
                
                // Nếu là thông báo hệ thống thì check readIds
                if (dict.ContainsKey("TargetId") && dict["TargetId"].ToString() == "all")
                {
                    isRead = readIds.Contains(doc.Id);
                }

                notifications.Add(new
                {
                    Id = doc.Id,
                    Title = dict.ContainsKey("Title") ? dict["Title"].ToString() : "",
                    Content = dict.ContainsKey("Content") ? dict["Content"].ToString() : "",
                    SenderId = dict.ContainsKey("SenderId") ? dict["SenderId"].ToString() : "",
                    SenderName = dict.ContainsKey("SenderName") ? dict["SenderName"].ToString() : "",
                    TargetId = dict.ContainsKey("TargetId") ? dict["TargetId"].ToString() : "",
                    CourseId = courseId,
                    Type = dict.ContainsKey("Type") ? dict["Type"].ToString() : "",
                    CreatedAt = dict.ContainsKey("CreatedAt") ? ((Google.Cloud.Firestore.Timestamp)dict["CreatedAt"]).ToDateTime() : DateTime.UtcNow,
                    IsRead = isRead
                });
            }

            // Sắp xếp giảm dần theo CreatedAt
            var sorted = notifications.OrderByDescending(n => (DateTime)((dynamic)n).CreatedAt).ToList();

            return Ok(sorted);
        }

        [HttpPut("read")]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkReadRequest request)
        {
            string uid = GetCurrentUserId();

            foreach (var id in request.NotificationIds)
            {
                var notifRef = _firestoreDb.Collection("Notifications").Document(id);
                var snap = await notifRef.GetSnapshotAsync();
                if (snap.Exists)
                {
                    var dict = snap.ToDictionary();
                    if (dict.ContainsKey("TargetId") && dict["TargetId"].ToString() == "all")
                    {
                        // Lưu vào sub-collection
                        await _firestoreDb.Collection("Users").Document(uid)
                            .Collection("ReadNotifications").Document(id)
                            .SetAsync(new { ReadAt = DateTime.UtcNow });
                    }
                    else
                    {
                        // Cập nhật trực tiếp
                        await notifRef.UpdateAsync("IsRead", true);
                    }
                }
            }

            return Ok(new { Message = "Notifications marked as read" });
        }

        [HttpPost("send")]
        [Authorize(Roles = "Instructor,Admin,Student")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                string uid = GetCurrentUserId();
                var notifData = new Dictionary<string, object>
                {
                    { "Title", request.Title },
                    { "Content", request.Content },
                    { "TargetId", request.TargetId },
                    { "CourseId", request.CourseId ?? "" },
                    { "Type", request.Type ?? "System" },
                    { "SenderId", string.IsNullOrEmpty(request.SenderId) ? uid : request.SenderId },
                    { "SenderName", request.SenderName ?? "" },
                    { "IsRead", false },
                    { "CreatedAt", DateTime.UtcNow }
                };

                await _firestoreDb.Collection("Notifications").AddAsync(notifData);
                return Ok(new { Message = "Notification sent." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error sending notification", Error = ex.Message });
            }
        }

        [HttpPost("send-to-class")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> SendToClass([FromBody] SendToClassRequest request)
        {
            try
            {
                string uid = GetCurrentUserId();

                // Lấy danh sách học sinh đã được duyệt vào lớp
                var regSnap = await _firestoreDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", request.CourseId)
                    .WhereEqualTo("status", "accepted")
                    .GetSnapshotAsync();

                var batch = _firestoreDb.StartBatch();
                var notifRef = _firestoreDb.Collection("Notifications");

                foreach (var doc in regSnap.Documents)
                {
                    string studentId = doc.GetValue<string>("userId");
                    var notifData = new Dictionary<string, object>
                    {
                        { "Title", request.Title },
                        { "Content", request.Content },
                        { "TargetId", studentId },
                        { "CourseId", request.CourseId },
                        { "Type", request.Type ?? "Course" },
                        { "SenderId", string.IsNullOrEmpty(request.SenderId) ? uid : request.SenderId },
                        { "SenderName", request.SenderName ?? "" },
                        { "IsRead", false },
                        { "CreatedAt", DateTime.UtcNow }
                    };
                    batch.Create(notifRef.Document(), notifData);
                }

                await batch.CommitAsync();
                return Ok(new { Message = "Notifications sent to class." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error sending class notification", Error = ex.Message });
            }
        }
    }

    public class MarkReadRequest
    {
        public List<string> NotificationIds { get; set; }
    }

    public class SendNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string? CourseId { get; set; }
        public string? Type { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
    }

    public class SendToClassRequest
    {
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? SenderId { get; set; }
        public string? SenderName { get; set; }
    }
}
