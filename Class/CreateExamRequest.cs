using System.Collections.Generic;

namespace e_learning_app.Class
{
    public class CreateExamRequest
    {
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        
        public double PassingScore { get; set; }
        public bool IsPublished { get; set; }
        public bool IsActive { get; set; }
        public bool AllowReview { get; set; }
        public bool RandomizeQuestions { get; set; }
        public bool ShowScore { get; set; }
        public bool AllowMultipleAttempts { get; set; }
        public int MaxAttempts { get; set; }

        public List<QuestionModel> Questions { get; set; } = new();
    }

    public class QuestionModel
    {
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectOptionIndex { get; set; }
        public double Points { get; set; } = 1.0;
    }
}
