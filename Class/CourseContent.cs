using Google.Cloud.Firestore;

namespace e_learning_app // or e_learning_app.Models
{
    [FirestoreData]
    public class CourseContent
    {
        // We don't map the ID directly to Firestore property, we set it manually after fetching
        public string Id { get; set; }

        [FirestoreProperty]
        public string CourseId { get; set; }

        [FirestoreProperty]
        public string Title { get; set; }

        [FirestoreProperty]
        public string Type { get; set; } // "Document", "Link", or "Note"

        [FirestoreProperty]
        public string Data { get; set; }

        [FirestoreProperty]
        public int OrderIndex { get; set; }

        // Computed property for UI, ignored by Firestore
        public string Icon => Type == "Document" ? "📄" : Type == "Link" ? "🔗" : "📝";
    }
}