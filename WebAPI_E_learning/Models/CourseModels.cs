namespace WebAPI_E_learning.Models
{
    public class CreateCourseRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0;
        public string Courseid { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string CourseType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public int StartPeriod { get; set; }
        public int EndPeriod { get; set; }
        public string Semester { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string AccentColor { get; set; } = string.Empty;
        public string InstructorId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int StudentCount { get; set; } = 0;
        public int AssignmentCount { get; set; } = 0;
    }

    public class UpdateCourseRequest : CreateCourseRequest { }

    public class CreateLessonRequest
    {
        public string Title { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string DocumentUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; } = 1;
    }

    public class UpdateLessonRequest : CreateLessonRequest { }

    public class CreateCourseContentRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public int OrderIndex { get; set; } = 0;
    }

    public class UpdateCourseContentRequest
    {
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Data { get; set; }
        public int? OrderIndex { get; set; }
    }
}
