using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace e_learning_app.Class
{
    /// <summary>
    /// Mô hình bài thi chính
    /// </summary>
    [FirestoreData]  
    public class Exam
    {
        [FirestoreDocumentId]  
        public string Id { get; set; }

        [FirestoreProperty]
        public string ClassId { get; set; }

        [FirestoreProperty]
        public string ClassName { get; set; }

        [FirestoreProperty]
        public string Title { get; set; }

        [FirestoreProperty]
        public string Description { get; set; }

        [FirestoreProperty]
        public string SubjectCode { get; set; }

        // ========== Cấu hình bài thi ==========
        [FirestoreProperty]
        public int TotalQuestions { get; set; }

        [FirestoreProperty]
        public int TimeLimitMinutes { get; set; }

        [FirestoreProperty]
        public double PassingScore { get; set; }

        [FirestoreProperty]
        public ExamType Type { get; set; }

        // ========== Các câu hỏi ==========
        [FirestoreProperty]
        public List<string> QuestionIds { get; set; }

        // ========== Trạng thái ==========
        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; }

        [FirestoreProperty]
        public DateTime? ScheduledDate { get; set; }

        [FirestoreProperty]
        public bool IsPublished { get; set; }

        [FirestoreProperty]
        public bool IsActive { get; set; }

        // ========== Cài đặt nâng cao ==========
        [FirestoreProperty]
        public bool AllowReview { get; set; }

        [FirestoreProperty]
        public bool RandomizeQuestions { get; set; }

        [FirestoreProperty]
        public bool ShowScore { get; set; }

        [FirestoreProperty]
        public bool AllowMultipleAttempts { get; set; }

        [FirestoreProperty]
        public int MaxAttempts { get; set; }

        public Exam()
        {
            Id = Guid.NewGuid().ToString();
            QuestionIds = new List<string>();
            CreatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
            UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
            TotalQuestions = 0;
            TimeLimitMinutes = 60;
            PassingScore = 50;
            Type = ExamType.Quiz;
            AllowReview = true;
            RandomizeQuestions = false;
            ShowScore = true;
            AllowMultipleAttempts = true;
            MaxAttempts = 3;
        }
    }

    /// <summary>
    /// Loại bài thi
    /// </summary>
    public enum ExamType
    {
        Quiz,           // Kiểm tra nhanh
        Midterm,        // Giữa kỳ
        Final,          // Cuối kỳ
        Practice,       // Luyện tập
        Assignment      // Bài tập
    }
}