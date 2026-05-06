using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class Lesson
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = null!;

        [FirestoreProperty]
        public string CourseId { get; set; } = null!;

        [FirestoreProperty]
        public string Title { get; set; } = null!;

        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty]
        public string VideoUrl { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
