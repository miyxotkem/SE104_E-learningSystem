namespace WebAPI_E_learning.Models
{
    public class CreateExamRequest
    {
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        
        // Advanced configurations
        public double PassingScore { get; set; }
        public bool IsPublished { get; set; }
        public bool IsActive { get; set; }
        public bool AllowReview { get; set; }
        public bool RandomizeQuestions { get; set; }
        public bool ShowScore { get; set; }
        public bool AllowMultipleAttempts { get; set; }
        public int MaxAttempts { get; set; }

        public System.Collections.Generic.List<QuestionModel> Questions { get; set; } = new();
    }

    public class QuestionModel
    {
        public string QuestionId { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> Options { get; set; } = new();
        public int CorrectOptionIndex { get; set; }
        public double Points { get; set; } = 1.0;
    }

    public class SubmitExamRequest
    {
        // Key: QuestionId or Index, Value: Index of Option chosen by student
        public System.Collections.Generic.Dictionary<string, int> Answers { get; set; } = new(); 
        public int TimeSpentSeconds { get; set; }
    }
}
