using Google.Cloud.Firestore;

namespace WebAPI_E_learning.Models
{
    [FirestoreData]
    public class ExamDraft
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty]
        public string ExamId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string StudentId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string StudentName { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime StartedAt { get; set; }

        [FirestoreProperty]
        public Dictionary<string, string> Answers { get; set; } = new();

        [FirestoreProperty]
        public List<string> MarkedForReview { get; set; } = new();

        [FirestoreProperty]
        public int LastQuestionIndex { get; set; }

        [FirestoreProperty]
        public DateTime SavedAt { get; set; }
    }
}
