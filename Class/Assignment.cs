using Google.Cloud.Firestore;
using System;

namespace e_learning_app.Class
{
    [FirestoreData]
    public class Assignment
    {
        [FirestoreDocumentId] public string Id { get; set; }
        [FirestoreProperty] public string CourseId { get; set; }
        [FirestoreProperty] public string Title { get; set; }
        [FirestoreProperty] public string Description { get; set; }
        [FirestoreProperty] public DateTime Deadline { get; set; }
        [FirestoreProperty] public string AttachedFileUrl { get; set; }
        [FirestoreProperty] public DateTime CreatedAt { get; set; }
        [FirestoreProperty] public bool IsGradesPublished { get; set; }
    }
}