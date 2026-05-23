using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using e_learning_app.Class;

namespace e_learning_app
{
    /// <summary>
    /// ViewModel bọc ExamQuestion để hỗ trợ inline editing trên UI
    /// </summary>
    public class QuestionEditModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ID gốc từ Firestore (null nếu câu hỏi mới)
        public string OriginalId { get; set; }
        public bool IsNew => string.IsNullOrEmpty(OriginalId);

        private int _order;
        public int Order { get => _order; set { _order = value; OnProp(); } }

        private string _content = "";
        public string Content { get => _content; set { _content = value; OnProp(); } }

        private string _optA = "";
        public string OptA { get => _optA; set { _optA = value; OnProp(); } }

        private string _optB = "";
        public string OptB { get => _optB; set { _optB = value; OnProp(); } }

        private string _optC = "";
        public string OptC { get => _optC; set { _optC = value; OnProp(); } }

        private string _optD = "";
        public string OptD { get => _optD; set { _optD = value; OnProp(); } }

        // "A", "B", "C", "D"
        private string _correctAnswer = "A";
        public string CorrectAnswer { get => _correctAnswer; set { _correctAnswer = value; OnProp(); } }

        private double _points = 1.0;
        public double Points { get => _points; set { _points = value; OnProp(); } }

        private bool _isExpanded = false;
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnProp(); } }

        /// <summary>
        /// Tạo QuestionEditModel từ ExamQuestion có sẵn
        /// </summary>
        public static QuestionEditModel FromExamQuestion(ExamQuestion q)
        {
            string correct = "A";
            if (q.CorrectAnswerIndex == 1) correct = "B";
            else if (q.CorrectAnswerIndex == 2) correct = "C";
            else if (q.CorrectAnswerIndex == 3) correct = "D";

            return new QuestionEditModel
            {
                OriginalId = q.Id,
                Order = q.QuestionOrder,
                Content = q.Content ?? "",
                OptA = q.Options?.Count > 0 ? q.Options[0] : "",
                OptB = q.Options?.Count > 1 ? q.Options[1] : "",
                OptC = q.Options?.Count > 2 ? q.Options[2] : "",
                OptD = q.Options?.Count > 3 ? q.Options[3] : "",
                CorrectAnswer = correct,
                Points = q.Points
            };
        }

        /// <summary>
        /// Convert ngược thành ExamQuestion để lưu vào Firestore
        /// </summary>
        public ExamQuestion ToExamQuestion()
        {
            int correctIdx = CorrectAnswer?.ToUpper() switch
            {
                "B" => 1, "C" => 2, "D" => 3, _ => 0
            };

            return new ExamQuestion
            {
                Id = IsNew ? Guid.NewGuid().ToString("N") : OriginalId,
                QuestionOrder = Order,
                Type = QuestionType.MultipleChoice,
                Content = Content?.Trim() ?? "",
                Options = new List<string>
                {
                    OptA?.Trim() ?? "",
                    OptB?.Trim() ?? "",
                    OptC?.Trim() ?? "",
                    OptD?.Trim() ?? ""
                },
                CorrectAnswerIndex = correctIdx,
                Points = Points
            };
        }
    }
}
