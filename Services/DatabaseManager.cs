using DocumentFormat.OpenXml.Bibliography;
using e_learning_app;
using e_learning_app.Class;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace e_learning_app
{
    /// <summary>
    /// DatabaseManager đã được refactor hoàn toàn sang REST API.
    /// Không còn phụ thuộc vào Firestore SDK hay firebase_json.json.
    /// Tất cả dữ liệu được truy cập qua ApiService.
    /// </summary>
    public class DatabaseManager
    {
        private User _currentUser = null;

        // Giữ lại GetDb dưới dạng null để các view cũ chưa refactor không lỗi biên dịch
        // Trong tương lai, xóa property này hoàn toàn
        public object GetDb => null;

        public User GetCurrentUser() => _currentUser;
        public void SetCurrentUser(User user) => _currentUser = user;

        public DatabaseManager()
        {
            Initialize();
        }

        public void Initialize()
        {
            // Không còn cần khởi tạo Firestore nữa.
            // ApiService đã tự cấu hình từ FirebaseService.Initialize()
        }

        // ==========================================
        // USER METHODS
        // ==========================================

        public async Task<string> AddUserAsync(User user)
        {
            try
            {
                var payload = new Dictionary<string, string>
                {
                    { "Uid", user.Id },
                    { "Email", user.Email },
                    { "FullName", user.FullName }
                };
                var response = await ApiService.PostAsync<dynamic>("users/sync-user", payload);
                return user.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddUserAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task<User> GetUserAsync(string documentId)
        {
            try
            {
                var response = await ApiService.GetAsync<UserResponse>($"users/{documentId}");
                if (response?.Data != null)
                {
                    response.Data.Id = response.Id;
                    return response.Data;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateFullProfile(string userId, User updatedUser)
        {
            try
            {
                var request = new { FullName = updatedUser.FullName };
                await ApiService.PutAsync($"users/profile", request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateFullProfile Error: {ex.Message}");
            }
        }

        // ==========================================
        // COURSE METHODS
        // ==========================================

        public async Task<bool> CreateCourseAsync(Course course)
        {
            try
            {
                var request = new
                {
                    Title = course.Title,
                    Description = course.Description,
                    Price = 0.0,
                    Courseid = course.Id,
                    ClassName = course.ClassName,

                    CourseType = course.CourseType,
                    Category = course.Category,

                    DayOfWeek = course.DayOfWeek,
                    StartPeriod = course.StartPeriod,
                    EndPeriod = course.EndPeriod,

                    Semester =course.Semester,
                    Emoji = course.Emoji,
                    AccentColor = course.AccentColor,
                    InstructorId = course.InstructorId,
                    CreatedAt = course.CreatedAt,
                    IsActive = true
                };
                var result = await ApiService.PostAsync("courses", request);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateCourseAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCourseAsync(Course course)
        {
            try
            {
                var request = new
                {
                    Title = course.Title ?? string.Empty,
                    Description = course.Description ?? string.Empty,
                    ThumbnailUrl = string.Empty,
                    Price = 0.0,
                    Courseid = course.Id ?? string.Empty,
                    ClassName = course.ClassName ?? string.Empty,
                    CourseType = course.CourseType ?? string.Empty,
                    Category = course.Category ?? string.Empty,
                    DayOfWeek = course.DayOfWeek ?? string.Empty,
                    StartPeriod = course.StartPeriod,
                    EndPeriod = course.EndPeriod,
                    Semester = course.Semester ?? string.Empty,
                    Emoji = course.Emoji ?? string.Empty,
                    AccentColor = course.AccentColor ?? string.Empty,
                    IsActive = course.IsActive,
                    InstructorId = course.InstructorId ?? string.Empty,
                    StudentCount = course.StudentCount,
                    AssignmentCount = course.AssignmentCount
                };
                return await ApiService.PutAsync($"courses/{course.Id}", request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCourseAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseAsync(string courseId)
        {
            try
            {
                return await ApiService.DeleteAsync($"courses/{courseId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteCourseAsync Error: {ex.Message}");
                return false;
            }
        }

        // ==========================================
        // LESSON METHODS
        // ==========================================

        public async Task<string> AddLessonAsync(Lesson lesson)
        {
            try
            {
                // Lessons Ä‘Æ°á»£c lÆ°u dÆ°á»›i dáº¡ng Contents cá»§a Course
                var request = new
                {
                    CourseId = lesson.CourseId,
                    Title = lesson.Title,
                    Type = "Video",
                    Data = lesson.VideoUrl,
                    OrderIndex = 0
                };
                var result = await ApiService.PostAsync<dynamic, dynamic>($"courses/{lesson.CourseId}/contents", request);
                return result?.ToString() ?? lesson.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddLessonAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Lesson>> GetLessonsByCourseAsync(string courseId)
        {
            try
            {
                // Láº¥y Contents tá»« API vÃ  map sang Lesson
                var contents = await ApiService.GetAsync<List<CourseContentResponse>>($"courses/{courseId}/contents");
                if (contents == null) return new List<Lesson>();

                return contents.Select(c => new Lesson
                {
                    Id = c.Id,
                    CourseId = courseId,
                    Title = c.Data?.Title ?? "",
                    VideoUrl = c.Data?.Data ?? "",
                    CreatedAt = c.Data?.CreatedAt ?? DateTime.MinValue
                }).OrderBy(l => l.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetLessonsByCourseAsync Error: {ex.Message}");
                return new List<Lesson>();
            }
        }

        public async Task<bool> DeleteLessonAsync(string lessonId)
        {
            try
            {
                return await ApiService.DeleteAsync($"lessons/{lessonId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteLessonAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Äáº¿m sá»‘ lÆ°á»£ng bÃ i ná»™p cá»§a má»™t bÃ i thi (kiá»ƒm tra trÆ°á»›c khi cho phÃ©p full edit)
        /// </summary>
        public async Task<List<Course>> GetAllCoursesAsync()
        {
            var response = await ApiService.GetAsync<List<CourseResponse>>("courses");
            if (response == null) return new List<Course>();
            return response.Select(c =>
            {
                var course = c.Data;
                if (course != null)
                {
                    course.Id = c.Id;
                }
                return course;
            }).Where(c => c != null).ToList();
        }
        public async Task<int> GetSubmissionCountByExamAsync(string examId) => 0;
        public async Task<List<e_learning_app.Class.ExamQuestion>> GetExamQuestionsAsync(string examId) => await ApiService.GetAsync<List<e_learning_app.Class.ExamQuestion>>($"exams/{examId}/questions") ?? new List<e_learning_app.Class.ExamQuestion>();
        public async Task<bool> UpdateExamAsync(Exam exam) => await ApiService.PutAsync($"exams/{exam.Id}", exam) != null;
        public async Task<bool> DeleteExamQuestionAsync(string examId, string questionId) => await ApiService.DeleteAsync($"exams/{examId}/questions/{questionId}") != null;
        public async Task<bool> SaveExamWithQuestionsAsync(Exam exam, List<e_learning_app.Class.ExamQuestion> questions)
        {
            var request = new
            {
                ExamId = exam.Id,
                ClassId = exam.ClassId,
                ClassName = exam.ClassName,
                Title = exam.Title,
                Description = exam.Description,
                TimeLimitMinutes = exam.TimeLimitMinutes,
                PassingScore = exam.PassingScore,
                IsPublished = exam.IsPublished,
                IsActive = exam.IsActive,
                AllowReview = exam.AllowReview,
                RandomizeQuestions = exam.RandomizeQuestions,
                ShowScore = exam.ShowScore,
                AllowMultipleAttempts = exam.AllowMultipleAttempts,
                MaxAttempts = exam.MaxAttempts,
                Questions = questions.Select(q => new {
                    QuestionId = q.Id,
                    QuestionText = q.Content,
                    Options = q.Options,
                    CorrectOptionIndex = q.CorrectAnswerIndex,
                    Points = q.Points
                }).ToList()
            };
            return await ApiService.PostAsync<object, Exam>($"exams/with-questions", request) != null;
        }
        public async Task<ExamDraft> GetExamDraftAsync(string examId, string studentId) => await ApiService.GetAsync<ExamDraft>($"exams/{examId}/drafts/{studentId}");
        public async Task DeleteExamDraftAsync(string examId, string studentId) => await ApiService.DeleteAsync($"exams/{examId}/drafts/{studentId}");
        public async Task<bool> SaveExamDraftAsync(ExamDraft draft) => await ApiService.PostAsync<ExamDraft, ExamDraft>($"exams/drafts", draft) != null;
    }
}
