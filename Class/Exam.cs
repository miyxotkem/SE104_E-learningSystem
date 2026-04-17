using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace e_learning_app.Class
{
    /// <summary>
    /// Mô hình bài thi chính
    /// </summary>
    [FirestoreData]
    public class Exam
    {
        [FirestoreProperty]
        public string Id { get; set; }                    // Unique ID (Firebase)
        
        [FirestoreProperty]
        public string ClassId { get; set; }              // Lớp học này thuộc
        
        [FirestoreProperty]
        public string Title { get; set; }                // Tên bài thi
        
        [FirestoreProperty]
        public string Description { get; set; }          // Mô tả chi tiết
        
        [FirestoreProperty]
        public string SubjectCode { get; set; }          // Mã môn học

        // Cấu hình bài thi
        [FirestoreProperty]
        public int TotalQuestions { get; set; }          // Tổng số câu
        
        [FirestoreProperty]
        public int TimeLimitMinutes { get; set; }        // Giới hạn thời gian (phút)
        
        [FirestoreProperty]
        public double PassingScore { get; set; }         // Điểm qua (%)
        
        [FirestoreProperty]
        public ExamType Type { get; set; }               // Loại: Quiz, Midterm, Final, etc.

        // Các câu hỏi
        [FirestoreProperty]
        public List<string> QuestionIds { get; set; }    // Danh sách ID câu hỏi

        // Trạng thái
        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }
        
        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; }
        
        [FirestoreProperty]
        public DateTime? ScheduledDate { get; set; }     // Thời gian thi
        
        [FirestoreProperty]
        public bool IsPublished { get; set; }            // Đã công bố?
        
        [FirestoreProperty]
        public bool IsActive { get; set; }               // Đang mở?

        // Cài đặt nâng cao
        [FirestoreProperty]
        public bool AllowReview { get; set; }            // Cho phép xem lại?
        
        [FirestoreProperty]
        public bool RandomizeQuestions { get; set; }     // Xáo trộn câu hỏi?
        
        [FirestoreProperty]
        public bool ShowScore { get; set; }              // Hiển thị điểm ngay?
        
        [FirestoreProperty]
        public bool AllowMultipleAttempts { get; set; }  // Cho phép thi nhiều lần?
        
        [FirestoreProperty]
        public int MaxAttempts { get; set; }             // Số lần thi tối đa

        public Exam()
        {
            Id = Guid.NewGuid().ToString();
            QuestionIds = new List<string>();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
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