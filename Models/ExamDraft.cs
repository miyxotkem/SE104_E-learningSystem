using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace e_learning_app.Class
{
    /// <summary>
    /// Lưu trạng thái bài làm tạm thời khi học sinh thoát giữa chừng.
    /// Timer vẫn tính dựa trên StartedAt (thực tế), không phải thời gian còn lại.
    /// </summary>
    [FirestoreData]
    public class ExamDraft
    {
        [FirestoreDocumentId]
        public string Id { get; set; }                    // "{ExamId}_{StudentId}"

        [FirestoreProperty]
        public string ExamId { get; set; }

        [FirestoreProperty]
        public string StudentId { get; set; }

        [FirestoreProperty]
        public string StudentName { get; set; }

        /// <summary>
        /// Thời điểm học sinh bắt đầu làm bài (UTC).
        /// Dùng để tính thời gian còn lại khi quay lại: remaining = limit - (now - StartedAt).
        /// </summary>
        [FirestoreProperty]
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Các câu trả lời đã chọn: QuestionId -> AnswerIndex (string)
        /// </summary>
        [FirestoreProperty]
        public Dictionary<string, string> Answers { get; set; }

        /// <summary>
        /// Các câu đã đánh dấu "xem lại"
        /// </summary>
        [FirestoreProperty]
        public List<string> MarkedForReview { get; set; }

        /// <summary>
        /// Index câu đang xem khi thoát
        /// </summary>
        [FirestoreProperty]
        public int LastQuestionIndex { get; set; }

        [FirestoreProperty]
        public DateTime SavedAt { get; set; }

        public ExamDraft()
        {
            Answers = new Dictionary<string, string>();
            MarkedForReview = new List<string>();
            SavedAt = DateTime.UtcNow;
        }
    }
}
