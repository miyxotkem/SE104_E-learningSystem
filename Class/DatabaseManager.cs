    using e_learning_app;
using e_learning_app.Class;
using Firebase.Auth;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using static Google.Cloud.Firestore.V1.StructuredQuery.Types;

namespace e_learning_app
{
    public class DatabaseManager
    {
        private FirestoreDb _db;
        private User _currentUser = null;

        public FirestoreDb GetDb => _db;

        public User GetCurrentUser() => _currentUser;
        public void SetCurrentUser(User user) => _currentUser = user;

        public DatabaseManager()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_db != null) return;

            // Dùng lại FirestoreDb đã được khởi tạo từ FirebaseService
            // (JSON đã được nhúng vào trong .exe, không cần đọc file ngoài nữa)
            if (FirebaseService.Db != null)
            {
                _db = FirebaseService.Db;
            }
            else
            {
                CustomDialog.Show("Firestore chưa được khởi tạo. Hãy đảm bảo FirebaseService.Initialize() đã được gọi trước.",
                                "Lỗi Kết Nối", DialogType.Error);
            }
        }

        // --- User Methods ---
        public async Task<string> AddUserAsync(User user)
        {
            CollectionReference usersRef = _db.Collection("Users");
            DocumentReference docRef = await usersRef.AddAsync(user);
            return docRef.Id;
        }

        public async Task<User> GetUserAsync(string documentId)
        {
            DocumentReference docRef = _db.Collection("Users").Document(documentId);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists ? snapshot.ConvertTo<User>() : null;
        }

        public async Task UpdateFullProfile(string userId, User updatedUser)
        {
            DocumentReference docRef = _db.Collection("Users").Document(userId);
            await docRef.SetAsync(updatedUser, SetOptions.Overwrite);
        }

        // --- Course Methods ---
        public async Task<bool> CreateCourseAsync(Course course)
        {
            try
            {
                if (_db == null) return false;
                await _db.Collection("Courses").Document(course.Id).SetAsync(course);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCourseAsync(Course course)
        {
            try
            {
                DocumentReference docRef = _db.Collection("Courses").Document(course.Id);

                await docRef.SetAsync(course, SetOptions.Overwrite);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseAsync(string courseId)
        {
            try
            {
                DocumentReference docRef = _db.Collection("Courses").Document(courseId);
                await docRef.DeleteAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete Error: {ex.Message}");
                return false;
            }
        }

        // --- Lesson Methods ---
        public async Task<string> AddLessonAsync(Lesson lesson)
        {
            try
            {
                CollectionReference lessonsRef = _db.Collection("Lessons");
                DocumentReference docRef = await lessonsRef.AddAsync(lesson);
                return docRef.Id;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Add Lesson Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Lesson>> GetLessonsByCourseAsync(string courseId)
        {
            var lessons = new List<Lesson>();
            try
            {
                // Remove OrderBy to avoid missing composite index errors on Firestore. Order in-memory instead.
                Query query = _db.Collection("Lessons").WhereEqualTo("CourseId", courseId);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var lesson = document.ConvertTo<Lesson>();
                        lesson.Id = document.Id;
                        lessons.Add(lesson);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get Lessons Error: {ex.Message}");
            }
            // Order by CreatedAt in memory
            return lessons.OrderBy(l => l.CreatedAt).ToList();
        }

        public async Task<bool> DeleteLessonAsync(string lessonId)
        {
            try
            {
                DocumentReference docRef = _db.Collection("Lessons").Document(lessonId);
                await docRef.DeleteAsync();

                // Optional: Xóa luôn comments của bài học này (nếu cần)
                // var commentsQuery = _db.Collection("Comments").WhereEqualTo("LessonId", lessonId);
                // var snapshot = await commentsQuery.GetSnapshotAsync();
                // foreach(var doc in snapshot.Documents) await doc.Reference.DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete Lesson Error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<CourseContent>> GetCourseContentsAsync(string courseId)
        {
            try
            {
                if (_db == null) return new List<CourseContent>();

                // Navigate to: Courses -> [courseId] -> Contents
                CollectionReference contentsRef = _db.Collection("Courses").Document(courseId).Collection("Contents");

                // Fetch them ordered by the OrderIndex we save during drag-and-drop
                QuerySnapshot snapshot = await contentsRef.OrderBy("OrderIndex").GetSnapshotAsync();

                List<CourseContent> contents = new List<CourseContent>();
                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        CourseContent content = document.ConvertTo<CourseContent>();
                        content.Id = document.Id; // Attach the Firestore generated ID
                        contents.Add(content);
                    }
                }
                return contents;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get Contents Error: {ex.Message}");
                return new List<CourseContent>();
            }
        }

        public async Task<bool> UpdateCourseContentOrderAsync(string courseId, List<CourseContent> contents)
        {
            try
            {
                if (_db == null) return false;

                // Use a WriteBatch to update all positions in a single transaction
                WriteBatch batch = _db.StartBatch();
                CollectionReference contentsRef = _db.Collection("Courses").Document(courseId).Collection("Contents");

                foreach (var content in contents)
                {
                    if (!string.IsNullOrEmpty(content.Id))
                    {
                        DocumentReference docRef = contentsRef.Document(content.Id);
                        // We only update the OrderIndex field to save bandwidth
                        batch.Update(docRef, "OrderIndex", content.OrderIndex);
                    }
                }

                await batch.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Order Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCourseContentAsync(string courseId, CourseContent content)
        {
            try
            {
                if (_db == null) return false;
                DocumentReference docRef = _db.Collection("Courses").Document(courseId).Collection("Contents").Document(content.Id);
                await docRef.SetAsync(content, SetOptions.Overwrite);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update Content Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseContentAsync(string courseId, string contentId)
        {
            try
            {
                if (_db == null) return false;
                DocumentReference docRef = _db.Collection("Courses").Document(courseId).Collection("Contents").Document(contentId);
                await docRef.DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete Lesson Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Delete Content Error: {ex.Message}");
                return false;
            }
        }

        // --- Comment Methods ---
        public async Task<string> AddCommentAsync(Comment comment)
        {
            try
            {
                CollectionReference commentsRef = _db.Collection("Comments");
                DocumentReference docRef = await commentsRef.AddAsync(comment);
                return docRef.Id;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Add Comment Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Comment>> GetCommentsByLessonAsync(string lessonId)
        {
            var comments = new List<Comment>();
            try
            {
                Query query = _db.Collection("Comments").WhereEqualTo("LessonId", lessonId);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var comment = document.ConvertTo<Comment>();
                        comment.Id = document.Id;
                        comments.Add(comment);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get Comments Error: {ex.Message}");
            }
            // Order by CreatedAt descending to show newest first, or ascending for chronological. We use descending here.
            return comments.OrderByDescending(c => c.CreatedAt).ToList();
        }

        // ========== EXAM METHODS ===
        /// <summary>
        /// Lấy tất cả lớp học của instructor hiện tại
        /// </summary>
        public async Task<List<Course>> GetAllCoursesAsync()
        {
            try
            {
                if (_db == null) return new List<Course>();

                var currentUser = GetCurrentUser();
                if (currentUser == null)
                {
                    return new List<Course>();
                }

                var query = await _db.Collection("Courses")
                    .WhereEqualTo("InstructorId", currentUser.Id)
                    .GetSnapshotAsync();

                var courses = query.Documents
                    .Select(doc => doc.ConvertTo<Course>())
                    .ToList();

                return courses;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllCoursesAsync Error: {ex.Message}");
                return new List<Course>();
            }
        }

        /// <summary>
        /// Tạo bài thi mới
        /// </summary>
        public async Task<bool> CreateExamAsync(Exam exam)
        {
            try
            {
                if (_db == null) return false;

                if (exam == null || string.IsNullOrEmpty(exam.Id))
                {
                    System.Diagnostics.Debug.WriteLine("❌ Exam or Exam.Id is null");
                    return false;
                }

                await _db.Collection("exams").Document(exam.Id).SetAsync(exam);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateExamAsync Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Lưu bài thi và danh sách câu hỏi cùng lúc sử dụng WriteBatch
        /// </summary>
        public async Task<bool> SaveExamWithQuestionsAsync(Exam exam, List<ExamQuestion> questions)
        {
            try
            {
                if (_db == null) return false;

                WriteBatch batch = _db.StartBatch();

                // Lưu thông tin bài thi chính
                DocumentReference examRef = _db.Collection("exams").Document(exam.Id);
                batch.Set(examRef, exam);

                // Lưu từng câu hỏi vào subcollection 'questions' của bài thi
                CollectionReference questionsRef = examRef.Collection("questions");
                foreach (var q in questions)
                {
                    DocumentReference qRef = questionsRef.Document(q.Id);
                    batch.Set(qRef, q);
                }

                await batch.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveExamWithQuestionsAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy tất cả câu hỏi của một bài thi
        /// </summary>
        public async Task<List<ExamQuestion>> GetExamQuestionsAsync(string examId)
        {
            try
            {
                if (_db == null) return new List<ExamQuestion>();

                var query = await _db.Collection("exams").Document(examId).Collection("questions").GetSnapshotAsync();
                return query.Documents.Select(doc => doc.ConvertTo<ExamQuestion>()).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetExamQuestionsAsync Error: {ex.Message}");
                return new List<ExamQuestion>();
            }
        }

        /// <summary>
        /// Tự động chấm điểm bài trắc nghiệm và lưu kết quả
        /// </summary>
        public async Task<ExamSubmission> AutoGradeAndSubmitExamAsync(ExamSubmission submission)
        {
            try
            {
                if (_db == null) return submission;

                // 1. Tải danh sách câu hỏi gốc từ Firebase
                var questions = await GetExamQuestionsAsync(submission.ExamId);
                
                double totalScore = 0;
                
                // 2. Chấm từng câu trả lời
                foreach (var answer in submission.Answers)
                {
                    var question = questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                    if (question != null && question.Type == QuestionType.MultipleChoice)
                    {
                        if (int.TryParse(answer.StudentAnswer, out int studentChoice))
                        {
                            if (studentChoice == question.CorrectAnswerIndex)
                            {
                                answer.IsCorrect = true;
                                answer.PointsEarned = question.Points;
                            }
                            else
                            {
                                answer.IsCorrect = false;
                                answer.PointsEarned = 0;
                            }
                        }
                        else
                        {
                            answer.IsCorrect = false;
                            answer.PointsEarned = 0;
                        }
                    }
                    totalScore += answer.PointsEarned;
                }

                // 3. Cập nhật tổng điểm và trạng thái
                submission.Score = totalScore;
                
                // Tính phần trăm điểm. Giả sử tổng điểm tối đa là tổng điểm của tất cả câu hỏi
                double maxPossibleScore = questions.Sum(q => q.Points);
                submission.Percentage = maxPossibleScore > 0 ? (totalScore / maxPossibleScore) * 100 : 0;
                
                submission.Status = SubmissionStatus.Graded;
                submission.GradedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);

                // 4. Lưu bài nộp lên Firestore (vì ExamSubmission đã có [FirestoreData] nên có thể lưu trực tiếp)
                await _db.Collection("exam_submissions").Document(submission.Id).SetAsync(submission);

                return submission;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoGradeAndSubmitExamAsync Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy tất cả bài nộp của một bài thi
        /// </summary>
        public async Task<List<ExamSubmission>> GetSubmissionsByExamAsync(string examId)
        {
            try
            {
                if (_db == null) return new List<ExamSubmission>();

                var query = await _db.Collection("exam_submissions")
                    .WhereEqualTo("ExamId", examId)
                    .GetSnapshotAsync();

                return query.Documents.Select(doc => doc.ConvertTo<ExamSubmission>()).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSubmissionsByExamAsync Error: {ex.Message}");
                return new List<ExamSubmission>();
            }
        }

        /// <summary>
        /// Lấy tất cả bài thi của lớp
        /// </summary>
        public async Task<List<Exam>> GetExamsByClassAsync(string classId)
        {
            try
            {
                if (_db == null) return new List<Exam>();

                var query = await _db.Collection("exams")
                    .WhereEqualTo("ClassId", classId)
                    .GetSnapshotAsync();

                return query.Documents
                    .Select(doc => doc.ConvertTo<Exam>())
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetExamsByClassAsync Error: {ex.Message}");
                return new List<Exam>();
            }
        }

        /// <summary>
        /// Lấy tất cả bài thi của giảng viên
        /// </summary>
        public async Task<List<Exam>> GetAllExamsForInstructorAsync()
        {
            try
            {
                if (_db == null) return new List<Exam>();
                
                var courses = await GetAllCoursesAsync();
                if (courses == null || courses.Count == 0) return new List<Exam>();

                var courseIds = courses.Select(c => c.Id).ToList();
                var allExams = new List<Exam>();

                // Firestore 'in' query supports max 10 items. So we chunk it.
                for (int i = 0; i < courseIds.Count; i += 10)
                {
                    var chunk = courseIds.Skip(i).Take(10).ToList();
                    var query = await _db.Collection("exams")
                        .WhereIn("ClassId", chunk)
                        .GetSnapshotAsync();

                    allExams.AddRange(query.Documents.Select(doc => doc.ConvertTo<Exam>()));
                }

                return allExams;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllExamsForInstructorAsync Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cập nhật bài thi
        /// </summary>
        public async Task<bool> UpdateExamAsync(Exam exam)
        {
            try
            {
                if (_db == null) return false;

                exam.UpdatedAt = DateTime.UtcNow;
                await _db.Collection("exams").Document(exam.Id).SetAsync(exam, SetOptions.Overwrite);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateExamAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa bài thi
        /// </summary>
        public async Task<bool> DeleteExamAsync(string examId)
        {
            try
            {
                if (_db == null) return false;

                await _db.Collection("exams").Document(examId).DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteExamAsync Error: {ex.Message}");
                return false;
            }
        }
    }
}


