using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty]
        public string Title { get; set; }

        [FirestoreProperty]
        public string Content { get; set; }

        [FirestoreProperty]
        public string SenderId { get; set; }

        [FirestoreProperty]
        public string SenderName { get; set; }

        [FirestoreProperty]
        public string TargetId { get; set; } // Có thể là UserId, CourseId hoặc "all"

        [FirestoreProperty]
        public string CourseId { get; set; } // Hỗ trợ chuyển hướng đến lớp học

        [FirestoreProperty]
        public string Type { get; set; } // "System", "Course", "Assignment", "Exam"

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public bool IsRead { get; set; } = false;

        // UI Helper
        public string TimeAgo
        {
            get
            {
                return CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }
        }

        public string Icon => Type switch
        {
            "System" => "📢",
            "Course" => "📚",
            "Assignment" => "📝",
            "Exam" => "⏱️",
            _ => "🔔"
        };
    }
}
