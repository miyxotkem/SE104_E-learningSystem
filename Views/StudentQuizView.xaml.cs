using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using e_learning_app.Class;

namespace e_learning_app.Views
{
    public partial class StudentQuizView : UserControl
    {
        private DatabaseManager _dbManager;

        public StudentQuizView()
        {
            InitializeComponent();
            _dbManager = new DatabaseManager(); // Should be passed in normally
        }

        private async void BtnSubmitTest_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;

            // --- MOCK DATA ---
            // Giả sử có một bài thi đã được tạo với ID "test_exam_123"
            // và ta tạo dữ liệu nộp bài giả lập
            string examId = "test_exam_123"; 

            var submission = new ExamSubmission
            {
                ExamId = examId,
                StudentId = "student_mock_001",
                StudentName = "Nguyễn Văn A",
                TimeSpentSeconds = 1200,
                Answers = new List<AnswerResponse>
                {
                    // Giả sử bài thi có 2 câu hỏi. Ta giả định ID của chúng.
                    new AnswerResponse { QuestionId = "q1", StudentAnswer = "1" }, // HS chọn đáp án B
                    new AnswerResponse { QuestionId = "q2", StudentAnswer = "3" }  // HS chọn đáp án D
                }
            };

            MessageBox.Show("Đang chấm điểm tự động...", "Thông báo");

            var gradedSubmission = await _dbManager.AutoGradeAndSubmitExamAsync(submission);

            if (gradedSubmission != null)
            {
                MessageBox.Show(
                    $"✅ Nộp bài thành công!\n\n" +
                    $"Điểm số: {gradedSubmission.Score}\n" +
                    $"Phần trăm: {gradedSubmission.Percentage}%\n" +
                    $"Trạng thái: {gradedSubmission.Status}",
                    "Kết quả chấm điểm", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("❌ Có lỗi xảy ra trong quá trình chấm điểm.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
