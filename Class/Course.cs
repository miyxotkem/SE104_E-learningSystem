using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class Course
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = null!;

        [FirestoreProperty]
        public string Title { get; set; } = null!;

        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty]
        public string ClassName { get; set; } = null!;

        [FirestoreProperty]
        public string Category { get; set; } = "General";

        [FirestoreProperty]
        public string Semester { get; set; } = null!;

        [FirestoreProperty]
        public string Emoji { get; set; } = "📚";

        [FirestoreProperty]
        public string AccentColor { get; set; } = "#3B82F6";

        [FirestoreProperty]
        public int StudentCount { get; set; }

        [FirestoreProperty]
        public string CourseType { get; set; } = "Chuyên ngành";

        [FirestoreProperty]
        public int AssignmentCount { get; set; }

        [FirestoreProperty]
        public bool IsActive { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string InstructorId { get; set; } = null!;

        [FirestoreProperty]
        public string DayOfWeek { get; set; } = "Hình thức 2";

        [FirestoreProperty]
        public int StartPeriod { get; set; }

        [FirestoreProperty]
        public int EndPeriod { get; set; }
    }
}