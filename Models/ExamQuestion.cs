using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace e_learning_app.Class
{
    /// <summary>
    /// Câu hỏi trong bài thi (với cấu hình riêng cho bài thi)
    /// </summary>
    [FirestoreData]
    public class ExamQuestion
    {
        [FirestoreDocumentId]
        public string Id { get; set; }                    // ID câu hỏi

        [FirestoreProperty]
        public int QuestionOrder { get; set; }            // Thứ tự câu hỏi

        [FirestoreProperty(ConverterType = typeof(FirestoreEnumNameConverter<QuestionType>))]
        public QuestionType Type { get; set; }            // Loại câu hỏi

        [FirestoreProperty]
        public string Content { get; set; }               // Nội dung câu hỏi

        // Cho câu trắc nghiệm
        [FirestoreProperty]
        public List<string> Options { get; set; }         // Các đáp án

        [FirestoreProperty]
        public int CorrectAnswerIndex { get; set; }       // Đáp án đúng (index)

        // Cho câu tự luận
        [FirestoreProperty]
        public int MaxWords { get; set; }                 // Tối da số từ

        // Điểm
        [FirestoreProperty]
        public double Points { get; set; }                // Điểm của câu (mặc định = 1)

        public ExamQuestion()
        {
            Id = Guid.NewGuid().ToString("N");
            Options = new List<string>();
            Points = 1;
        }
    }

    /// <summary>
    /// Loại câu hỏi
    /// </summary>
    public enum QuestionType
    {
        MultipleChoice,     // Trắc nghiệm
        TrueFalse,         // Đúng/Sai
        ShortAnswer,       // Câu trả lời ngắn
        Essay,             // Tự luận
        Matching           // Ghép đôi
    }
}