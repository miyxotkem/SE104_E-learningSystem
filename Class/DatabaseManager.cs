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
                    Title = course.Title,
                    Description = course.Description,
                    ThumbnailUrl = (string)null,
                    Price = 0.0
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
                // Lessons được lưu dưới dạng Contents của Course
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
                // Lấy Contents từ API và map sang Lesson
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
                Debug.WriteLine($"DeleteLessonAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateLessonAsync(Lesson lesson)
        {
            try
            {
                var request = new
                {
                    Title = lesson.Title,
                    Data = lesson.VideoUrl,
                    Type = "Video"
                };
                return await ApiService.PutAsync($"courses/{lesson.CourseId}/contents/{lesson.Id}", request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateLessonAsync Error: {ex.Message}");
                return false;
            }
        }

        // ==========================================
        // COURSE CONTENTS
        // ==========================================

        public async Task<List<CourseContent>> GetCourseContentsAsync(string courseId)
        {
            try
            {
                var response = await ApiService.GetAsync<List<CourseContentResponse>>($"courses/{courseId}/contents");
                if (response == null) return new List<CourseContent>();

                return response.Select(c =>
                {
                    var content = new CourseContent
                    {
                        Id = c.Id,
                        CourseId = c.Data?.CourseId ?? string.Empty,
                        Title = c.Data?.Title ?? string.Empty,
                        Type = c.Data?.Type ?? string.Empty,
                        Data = c.Data?.Data ?? string.Empty,
                        OrderIndex = c.Data?.OrderIndex ?? 0
                    };
                    return content;
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetCourseContentsAsync Error: {ex.Message}");
                return new List<CourseContent>();
            }
        }

        public async Task<bool> UpdateCourseContentOrderAsync(string courseId, List<CourseContent> contents)
        {
            try
            {
                bool allOk = true;
                foreach (var content in contents)
                {
                    if (!string.IsNullOrEmpty(content.Id))
                    {
                        var req = new { OrderIndex = content.OrderIndex };
                        bool ok = await ApiService.PutAsync($"courses/{courseId}/contents/{content.Id}", req);
                        if (!ok) allOk = false;
                    }
                }
                return allOk;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCourseContentOrderAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCourseContentAsync(string courseId, CourseContent content)
        {
            try
            {
                var req = new
                {
                    Title = content.Title,
                    Type = content.Type,
                    Data = content.Data,
                    OrderIndex = (int?)content.OrderIndex
                };
                return await ApiService.PutAsync($"courses/{courseId}/contents/{content.Id}", req);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCourseContentAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCourseContentAsync(string courseId, string contentId)
        {
            try
            {
                return await ApiService.DeleteAsync($"courses/{courseId}/contents/{contentId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteCourseContentAsync Error: {ex.Message}");
                return false;
            }
        }

        // ==========================================
        // COMMENT METHODS
        // ==========================================

        public async Task<string> AddCommentAsync(Comment comment)
        {
            try
            {
                var req = new
                {
                    LessonId = comment.LessonId,
                    UserId = comment.UserId,
                    UserName = comment.UserName,
                    Content = comment.Content,
                    ParentId = comment.ParentId
                };
                var result = await ApiService.PostAsync<dynamic, dynamic>($"comments", req);
                return result?.ToString() ?? Guid.NewGuid().ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddCommentAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Comment>> GetCommentsByLessonAsync(string lessonId)
        {
            try
            {
                var response = await ApiService.GetAsync<List<Comment>>($"comments/{lessonId}");
                return response ?? new List<Comment>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetCommentsByLessonAsync Error: {ex.Message}");
                return new List<Comment>();
            }
        }

        // ==========================================
        // EXAM METHODS
        // ==========================================

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            try
            {
                var response = await ApiService.GetAsync<List<CourseResponse>>("courses");
                if (response == null) return new List<Course>();

                return response.Select(c =>
                {
                    var course = c.Data ?? new Course();
                    course.Id = c.Id;
                    return course;
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllCoursesAsync Error: {ex.Message}");
                return new List<Course>();
            }
        }

        public async Task<bool> CreateExamAsync(Exam exam)
        {
            try
            {
                if (exam == null || string.IsNullOrEmpty(exam.Id))
                {
                    Debug.WriteLine("❌ Exam or Exam.Id is null");
                    return false;
                }

                var req = new
                {
                    CourseId = exam.ClassId,
                    Title = exam.Title,
                    Description = exam.Description,
                    DurationMinutes = exam.TimeLimitMinutes,
                    PassingScore = exam.PassingScore,
                    IsPublished = exam.IsPublished,
                    AllowReview = exam.AllowReview,
                    RandomizeQuestions = exam.RandomizeQuestions,
                    ShowScore = exam.ShowScore,
                    AllowMultipleAttempts = exam.AllowMultipleAttempts,
                    MaxAttempts = exam.MaxAttempts,
                    Questions = new List<object>() // Sẽ lưu câu hỏi qua SaveExamWithQuestionsAsync
                };
                return await ApiService.PostAsync("exams", req);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateExamAsync Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SaveExamWithQuestionsAsync(Exam exam, List<ExamQuestion> questions)
        {
            try
            {
                var questionsList = questions.Select(q => new
                {
                    QuestionText = q.Content,
                    Options = q.Options,
                    CorrectOptionIndex = q.CorrectAnswerIndex,
                    Points = q.Points
                }).ToList();

                var req = new
                {
                    CourseId = exam.ClassId,
                    Title = exam.Title,
                    Description = exam.Description,
                    DurationMinutes = exam.TimeLimitMinutes,
                    PassingScore = exam.PassingScore,
                    IsPublished = exam.IsPublished,
                    AllowReview = exam.AllowReview,
                    RandomizeQuestions = exam.RandomizeQuestions,
                    ShowScore = exam.ShowScore,
                    AllowMultipleAttempts = exam.AllowMultipleAttempts,
                    MaxAttempts = exam.MaxAttempts,
                    Questions = questionsList
                };
                return await ApiService.PostAsync("exams", req);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveExamWithQuestionsAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<ExamQuestion>> GetExamQuestionsAsync(string examId)
        {
            try
            {
                // ExamsController trả về toàn bộ exam kèm câu hỏi
                var response = await ApiService.GetAsync<JsonElement>($"exams/{examId}");
                var questions = new List<ExamQuestion>();

                if (response.ValueKind != JsonValueKind.Undefined &&
                    response.TryGetProperty("Data", out var data) &&
                    data.TryGetProperty("Questions", out var questionsArr) &&
                    questionsArr.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var q in questionsArr.EnumerateArray())
                    {
                        questions.Add(new ExamQuestion
                        {
                            Id = i.ToString(),
                            Content = q.TryGetProperty("QuestionText", out var qt) ? qt.GetString() : "",
                            CorrectAnswerIndex = q.TryGetProperty("CorrectOptionIndex", out var ci) ? ci.GetInt32() : 0,
                            Points = q.TryGetProperty("Points", out var pt) ? pt.GetDouble() : 1,
                            Options = q.TryGetProperty("Options", out var opts) && opts.ValueKind == JsonValueKind.Array
                                ? opts.EnumerateArray().Select(o => o.GetString()).ToList()
                                : new List<string>(),
                            Type = QuestionType.MultipleChoice
                        });
                        i++;
                    }
                }

                return questions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetExamQuestionsAsync Error: {ex.Message}");
                return new List<ExamQuestion>();
            }
        }

        public async Task<ExamSubmission> AutoGradeAndSubmitExamAsync(ExamSubmission submission)
        {
            try
            {
                var answers = new Dictionary<string, int>();
                foreach (var answer in submission.Answers)
                {
                    if (int.TryParse(answer.StudentAnswer, out int studentChoice))
                        answers[answer.QuestionId] = studentChoice;
                }

                var req = new { Answers = answers };
                var result = await ApiService.PostAsync<dynamic, JsonElement>($"exams/{submission.ExamId}/submit", req);

                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    if (result.TryGetProperty("Score", out var score))
                        submission.Score = score.GetInt32();
                    if (result.TryGetProperty("TotalQuestions", out var total))
                        submission.Percentage = submission.Score / (double)total.GetInt32() * 100;
                    submission.Status = SubmissionStatus.Graded;
                    submission.GradedAt = DateTime.UtcNow;
                }

                return submission;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoGradeAndSubmitExamAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ExamSubmission>> GetSubmissionsByExamAsync(string examId)
        {
            try
            {
                var response = await ApiService.GetAsync<List<ExamSubmission>>($"exams/{examId}/submissions");
                return response ?? new List<ExamSubmission>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetSubmissionsByExamAsync Error: {ex.Message}");
                return new List<ExamSubmission>();
            }
        }

        public async Task<List<Exam>> GetExamsByClassAsync(string classId)
        {
            try
            {
                var response = await ApiService.GetAsync<List<JsonElement>>($"exams/course/{classId}");
                if (response == null) return new List<Exam>();

                return response.Select(e =>
                {
                    var exam = new Exam { Id = e.TryGetProperty("Id", out var id) ? id.GetString() : "" };
                    if (e.TryGetProperty("Data", out var data))
                    {
                        if (data.TryGetProperty("Title", out var t)) exam.Title = t.GetString();
                        if (data.TryGetProperty("ClassId", out var c)) exam.ClassId = c.GetString();
                    }
                    return exam;
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetExamsByClassAsync Error: {ex.Message}");
                return new List<Exam>();
            }
        }

        public async Task<List<Exam>> GetAllExamsForInstructorAsync()
        {
            try
            {
                var response = await ApiService.GetAsync<List<JsonElement>>("exams");
                if (response == null) return new List<Exam>();

                return response.Select(e =>
                {
                    var exam = new Exam { Id = e.TryGetProperty("Id", out var id) ? id.GetString() : "" };
                    if (e.TryGetProperty("Data", out var data))
                    {
                        if (data.TryGetProperty("Title", out var t)) exam.Title = t.GetString();
                        if (data.TryGetProperty("ClassId", out var c)) exam.ClassId = c.GetString();
                        if (data.TryGetProperty("IsPublished", out var pub)) exam.IsPublished = pub.GetBoolean();
                    }
                    return exam;
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllExamsForInstructorAsync Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateExamAsync(Exam exam)
        {
            try
            {
                var req = new Dictionary<string, object>
                {
                    { "Title", exam.Title },
                    { "IsPublished", exam.IsPublished },
                    { "DurationMinutes", exam.TimeLimitMinutes },
                };
                return await ApiService.PutAsync($"exams/{exam.Id}", req);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateExamAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteExamAsync(string examId)
        {
            try
            {
                return await ApiService.DeleteAsync($"exams/{examId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteExamAsync Error: {ex.Message}");
                return false;
            }
        }
    }
}
