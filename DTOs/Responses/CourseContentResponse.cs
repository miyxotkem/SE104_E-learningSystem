using System;

namespace e_learning_app.Class
{
    /// <summary>
    /// DTO nhận từ API GET /api/courses/{id}/contents
    /// </summary>
    public class CourseContentResponse
    {
        public string Id { get; set; } = string.Empty;
        public CourseContentData Data { get; set; }
    }

    public class CourseContentData
    {
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
