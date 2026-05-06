using Google.Cloud.Firestore;
using System;

namespace e_learning_app.Class
{
    [FirestoreData]
    public class Submission
    {
        [FirestoreDocumentId] public string Id { get; set; }
        [FirestoreProperty] public string AssignmentId { get; set; }
        [FirestoreProperty] public string StudentId { get; set; }
        [FirestoreProperty] public string FileUrl { get; set; }
        [FirestoreProperty] public DateTime SubmittedAt { get; set; }
        [FirestoreProperty] public bool IsLate { get; set; }
    }
}