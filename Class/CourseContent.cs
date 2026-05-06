using Google.Cloud.Firestore;

namespace e_learning_app
{
    [FirestoreData]
    public class CourseContent
    {
        public string Id { get; set; }

        [FirestoreProperty]
        public string CourseId { get; set; }

        [FirestoreProperty]
        public string Title { get; set; }

        [FirestoreProperty]
        public string Type { get; set; }

        [FirestoreProperty]
        public string Data { get; set; }

        [FirestoreProperty]
        public int OrderIndex { get; set; }

        public string Icon => Type == "Document" ? "📄" : Type == "Link" ? "🔗" : "📝";
    }
}