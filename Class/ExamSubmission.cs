using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace e_learning_app.Class
{
    /// <summary>
    /// Kết quả làm bài của học sinh
    /// </summary>
    [FirestoreData]
    public class ExamSubmission
    {
        [FirestoreDocumentId]
        public string Id { get; set; }                    // Unique ID

        [FirestoreProperty]
        public string ExamId { get; set; }                // Bài thi

        [FirestoreProperty]
        public string StudentId { get; set; }             // Học sinh

        [FirestoreProperty]
        public string StudentName { get; set; }           // Tên học sinh

        // Thời gian
        [FirestoreProperty]
        public DateTime SubmittedAt { get; set; }         // Nộp bài lúc

        [FirestoreProperty]
        public int TimeSpentSeconds { get; set; }         // Thời gian làm bài (giây)

        // Kết quả
        [FirestoreProperty]
        public List<AnswerResponse> Answers { get; set; }  // Các câu trả lời

        [FirestoreProperty]
        public double Score { get; set; }                  // Điểm số

        [FirestoreProperty]
        public double Percentage { get; set; }             // Phần trăm (%)

        [FirestoreProperty(ConverterType = typeof(FirestoreEnumNameConverter<SubmissionStatus>))]
        public SubmissionStatus Status { get; set; }       // Trạng thái

        // Nhận xét
        [FirestoreProperty]
        public string FeedbackFromTeacher { get; set; }    // Nhận xét giáo viên

        [FirestoreProperty]
        public DateTime? GradedAt { get; set; }            // Lúc chấm điểm

        public ExamSubmission()
        {
            Id = Guid.NewGuid().ToString("N");
            Answers = new List<AnswerResponse>();
            SubmittedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
            Status = SubmissionStatus.Submitted;
        }
    }

    /// <summary>
    /// Câu trả lời của học sinh
    /// </summary>
    [FirestoreData]
    public class AnswerResponse
    {
        [FirestoreProperty]
        public string QuestionId { get; set; }

        [FirestoreProperty]
        public int QuestionOrder { get; set; }

        [FirestoreProperty]
        public string StudentAnswer { get; set; }         // Câu trả lời (text hoặc index)

        [FirestoreProperty]
        public bool? IsCorrect { get; set; }              // Đúng/Sai (null nếu chưa chấm)

        [FirestoreProperty]
        public double PointsEarned { get; set; }          // Điểm đạt được
    }

    /// <summary>
    /// Trạng thái nộp bài
    /// </summary>
    public enum SubmissionStatus
    {
        InProgress,         // Đang làm
        Submitted,          // Đã nộp
        Graded,             // Đã chấm
        Expired             // Hết hạn
    }
}