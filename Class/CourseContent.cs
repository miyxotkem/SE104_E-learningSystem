namespace e_learning_app
{
    public class CourseContent
    {
        public string Id { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public int OrderIndex { get; set; }

        public string Icon => Type == "Document" ? "📄" : Type == "Link" ? "🔗" : "📝";
    }
}