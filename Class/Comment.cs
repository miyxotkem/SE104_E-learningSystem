using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class Comment
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = null!;

        [FirestoreProperty]
        public string LessonId { get; set; } = null!;

        [FirestoreProperty]
        public string UserId { get; set; } = null!;

        [FirestoreProperty]
        public string UserName { get; set; } = null!;

        [FirestoreProperty]
        public string UserRole { get; set; } = null!; // Student, Teacher

        [FirestoreProperty]
        public string Content { get; set; } = null!;

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
