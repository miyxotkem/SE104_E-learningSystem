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
        private DateTime _deadline;
        [FirestoreProperty] 
        public DateTime Deadline 
        { 
            get => (_deadline != default && _deadline != DateTime.MinValue) ? _deadline : DueDate; 
            set => _deadline = value; 
        }
        [FirestoreProperty] public DateTime DueDate { get; set; }
        [FirestoreProperty] public string AttachedFileUrl { get; set; }
        [FirestoreProperty] public DateTime CreatedAt { get; set; }
        [FirestoreProperty] public bool IsGradesPublished { get; set; }
    }
}