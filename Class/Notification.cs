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
                var diff = DateTime.UtcNow - CreatedAt;
                if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays} ngày trước";
                if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours} giờ trước";
                if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes} phút trước";
                return "Vừa xong";
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
