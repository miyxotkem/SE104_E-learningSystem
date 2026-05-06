using e_learning_app.Class;
using Firebase.Auth;
using Firebase.Auth.Providers;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Api;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static Google.Apis.Auth.OAuth2.Web.AuthorizationCodeWebApp;

namespace e_learning_app
{
    public static class FirebaseService
    {
        private const string ApiKey = "AIzaSyDU5RuicqibEcEx5dmagllQ14WOJ467yic";
        private const string ProjectId = "e-learning-cd1b3";
        private static FirebaseAuthClient ?_authClient;
        public static FirestoreDb ?Db { get; private set; }

        /// <summary>
        /// Đọc file JSON được nhúng vào trong .exe (Embedded Resource)
        /// </summary>
        private static string GetEmbeddedJson(string resourceName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string fullName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase)) ?? "";

            if (string.IsNullOrEmpty(fullName)) return "";

            using (Stream? stream = assembly.GetManifestResourceStream(fullName))
            {
                if (stream == null) return "";
                using (StreamReader reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        public static void Initialize()
        {
            try
            {
                // 1. Khởi tạo Auth Client
                var config = new FirebaseAuthConfig
                {
                    ApiKey = ApiKey,
                    AuthDomain = $"{ProjectId}.firebaseapp.com",
                    Providers = new FirebaseAuthProvider[]
                    {
                        new EmailProvider(),
                        new GoogleProvider()
                    }
                };
                _authClient = new FirebaseAuthClient(config);

                // 2. Khởi tạo Firestore bằng JSON nhúng trong .exe
                string serviceAccountJson = GetEmbeddedJson("firebase_json.json");
                if (!string.IsNullOrEmpty(serviceAccountJson))
                {
                    var credential = GoogleCredential
                        .FromJson(serviceAccountJson)
                        .CreateScoped("https://www.googleapis.com/auth/cloud-platform",
                                      "https://www.googleapis.com/auth/datastore");

                    var firestoreClient = new Google.Cloud.Firestore.V1.FirestoreClientBuilder
                    {
                        Credential = credential
                    }.Build();

                    Db = FirestoreDb.Create(ProjectId, firestoreClient);
                }
                else
                {
                    MessageBox.Show("Không tìm thấy file cấu hình Firebase trong ứng dụng!", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khởi tạo Firebase: " + ex.Message);
            }
        }

        public static async Task<string> LoginAsync(string email, string password)
        {
            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);
                return userCredential.User.Uid;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static async Task<string> RegisterAsync(string email, string password)
        {
            try
            {
                if (_authClient == null) return null;
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);

                return userCredential.User.Uid;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi đăng ký: " + ex.Message);
                return null;
            }
        }

        //GOOGLEEEEEEEEEEEEE
        private const string GoogleClientId = "105514257729-ienl99san19bis48vav5lppchd7fuf1j.apps.googleusercontent.com";
        private const string GoogleClientSecret = "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";
        public static async Task<Firebase.Auth.User> LoginWithGoogleAsync()
        {
            string credPath = "gg.auth.api";
            var dataStore = new FileDataStore(credPath, true);

            try
            {
                // Xóa cache cũ để có thể chọn tài khoản Google khác
                await dataStore.ClearAsync();

                // Đọc Client Secret từ file được nhúng trong .exe
                string googleJsonContent = GetEmbeddedJson("google_json.json");
                ClientSecrets secrets;

                if (!string.IsNullOrEmpty(googleJsonContent))
                {
                    // Đọc trực tiếp từ JSON nhúng trong .exe
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(googleJsonContent));
                    var clientSecrets = await GoogleClientSecrets.FromStreamAsync(stream);
                    secrets = clientSecrets.Secrets;
                }
                else
                {
                    // Fallback: dùng hardcoded (phòng trường hợp resource không tìm thấy)
                    secrets = new ClientSecrets
                    {
                        ClientId = GoogleClientId,
                        ClientSecret = GoogleClientSecret
                    };
                }

                string[] scopes = { "openid", "email", "profile" };
                var googleCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    dataStore);

                string idToken = googleCredential.Token.IdToken;

                if (_authClient == null) return null;

                var credential = GoogleProvider.GetCredential(idToken, OAuthCredentialTokenType.IdToken);
                var authResult = await _authClient.SignInWithCredentialAsync(credential);
                return authResult.User;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("stale") || ex.Message.Contains("INVALID_ID_RESPONSE"))
                {
                    await dataStore.ClearAsync();
                    MessageBox.Show("Phiên đăng nhập cũ bị lỗi. Hệ thống đã tự động làm mới, vui lòng nhấn Đăng nhập lại một lần nữa nhé!", "Thông báo");
                }
                else
                {
                    MessageBox.Show("Lỗi đăng nhập Google: " + ex.Message);
                }
                return null;
            }
        }

        //reset pass
        public static async Task<bool> SendPasswordResetAsync(string email)
        {
            try
            {
                await _authClient.ResetEmailPasswordAsync(email);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<string> GetUserRoleAsync(string uid)
        {
            try
            {
                if (Db == null) return "Student";
                DocumentSnapshot snapshot = await Db.Collection("userss").Document(uid).GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    var data = snapshot.ToDictionary();
                    if (data.ContainsKey("Role")) return data["Role"]?.ToString() ?? "Student";
                }
                return "Student";
            }
            catch
            {
                return "Student";
            }
        }

        public static async Task<bool> CreateUserInFirestore(string uid, string email = "", string displayName = "")
        {
            try
            {
                if (Db == null)
                {
                    MessageBox.Show("Chưa kết nối được Firestore!");
                    return false;
                }

                DocumentReference docRef = Db.Collection("users").Document(uid);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                string role = email == "buitrantrongnguyen@gmail.com" ? "Teacher" : "Student";

                // Kiểm tra xem user đã tồn tại chưa để tránh ghi đè
                if (!snapshot.Exists)
                {
                    Dictionary<string, object> user = new Dictionary<string, object>
                    {
                        { "Uid", uid },
                        { "Email", email },
                        { "DisplayName", string.IsNullOrEmpty(displayName) ? "New User" : displayName },
                        { "CreatedAt", FieldValue.ServerTimestamp },
                        { "Role", role }
                    };

                    await docRef.SetAsync(user);
                }
                else
                {
                    await docRef.UpdateAsync(new Dictionary<string, object> { { "Role", role }, { "Email", email } });
                }
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tạo user trong Firestore: " + ex.Message);
                return false;
            }
        }
        // ===== EXAM CRUD OPERATIONS =====

        /// <summary>
        /// Tạo bài thi mới
        /// </summary>
        public static async Task<bool> CreateExamAsync(Exam exam)
        {
            try
            {
                if (Db == null) return false;

                var examData = new Dictionary<string, object>
                {
                    { "id", exam.Id },
                    { "classId", exam.ClassId },
                    { "title", exam.Title },
                    { "description", exam.Description ?? "" },
                    { "subjectCode", exam.SubjectCode },
                    { "totalQuestions", exam.TotalQuestions },
                    { "timeLimitMinutes", exam.TimeLimitMinutes },
                    { "passingScore", exam.PassingScore },
                    { "type", exam.Type.ToString() },
                    { "questionIds", exam.QuestionIds },
                    { "createdAt", exam.CreatedAt },
                    { "updatedAt", exam.UpdatedAt },
                    { "scheduledDate", exam.ScheduledDate },
                    { "isPublished", exam.IsPublished },
                    { "isActive", exam.IsActive },
                    { "allowReview", exam.AllowReview },
                    { "randomizeQuestions", exam.RandomizeQuestions },
                    { "showScore", exam.ShowScore },
                    { "allowMultipleAttempts", exam.AllowMultipleAttempts },
                    { "maxAttempts", exam.MaxAttempts }
                };

                await Db.Collection("exams").Document(exam.Id).SetAsync(examData);
                return true;
            }
            catch(Exception ex)
            { 
                MessageBox.Show($"Lỗi tạo bài thi: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy tất cả bài thi của lớp
        /// </summary>
        public static async Task<List<Exam>> GetExamsByClassAsync(string classId)
        {
            try
            {
                if (Db == null) return new List<Exam>();

                var query = await Db.Collection("exams")
                    .WhereEqualTo("classId", classId)
                    .GetSnapshotAsync();

                var exams = new List<Exam>();
                foreach (var doc in query.Documents)
                {
                    exams.Add(MapToExam(doc));
                }
                return exams;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lấy bài thi: {ex.Message}");
                return new List<Exam>();
            }
        }

        /// <summary>
        /// Cập nhật bài thi
        /// </summary>
        public static async Task<bool> UpdateExamAsync(Exam exam)
        {
            try
            {
                if (Db == null) return false;

                exam.UpdatedAt = DateTime.Now;

                var examData = new Dictionary<string, object>
                {
                    { "title", exam.Title },
                    { "description", exam.Description ?? "" },
                    { "totalQuestions", exam.TotalQuestions },
                    { "timeLimitMinutes", exam.TimeLimitMinutes },
                    { "passingScore", exam.PassingScore },
                    { "questionIds", exam.QuestionIds },
                    { "updatedAt", exam.UpdatedAt },
                    { "scheduledDate", exam.ScheduledDate },
                    { "isPublished", exam.IsPublished },
                    { "isActive", exam.IsActive }
                };

                await Db.Collection("exams").Document(exam.Id).UpdateAsync(examData);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật bài thi: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa bài thi
        /// </summary>
        public static async Task<bool> DeleteExamAsync(string examId)
        {
            try
            {
                if (Db == null) return false;
                await Db.Collection("exams").Document(examId).DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xóa bài thi: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lưu kết quả làm bài
        /// </summary>
        public static async Task<bool> SubmitExamAsync(ExamSubmission submission)
        {
            try
            {
                if (Db == null) return false;

                var submissionData = new Dictionary<string, object>
                {
                    { "id", submission.Id },
                    { "examId", submission.ExamId },
                    { "studentId", submission.StudentId },
                    { "studentName", submission.StudentName },
                    { "submittedAt", submission.SubmittedAt },
                    { "timeSpentSeconds", submission.TimeSpentSeconds },
                    { "score", submission.Score },
                    { "percentage", submission.Percentage },
                    { "status", submission.Status.ToString() }
                };

                await Db.Collection("exam_submissions").Document(submission.Id).SetAsync(submissionData);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nộp bài: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy kết quả của học sinh
        /// </summary>
        public static async Task<List<ExamSubmission>> GetStudentSubmissionsAsync(string studentId)
        {
            try
            {
                if (Db == null) return new List<ExamSubmission>();

                var query = await Db.Collection("exam_submissions")
                    .WhereEqualTo("studentId", studentId)
                    .GetSnapshotAsync();

                var submissions = new List<ExamSubmission>();
                foreach (var doc in query.Documents)
                {
                    submissions.Add(MapToSubmission(doc));
                }
                return submissions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lấy kết quả: {ex.Message}");
                return new List<ExamSubmission>();
            }
        }

        private static Exam MapToExam(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();
            return new Exam
            {
                Id = doc.Id,
                ClassId = data.ContainsKey("classId") ? data["classId"]?.ToString() ?? "" : "",
                Title = data.ContainsKey("title") ? data["title"]?.ToString() ?? "" : "",
                Description = data.ContainsKey("description") ? data["description"]?.ToString() ?? "" : "",
                SubjectCode = data.ContainsKey("subjectCode") ? data["subjectCode"]?.ToString() ?? "" : "",
                TotalQuestions = data.ContainsKey("totalQuestions") ? Convert.ToInt32(data["totalQuestions"] ?? 0) : 0,
                TimeLimitMinutes = data.ContainsKey("timeLimitMinutes") ? Convert.ToInt32(data["timeLimitMinutes"] ?? 60) : 60,
                PassingScore = data.ContainsKey("passingScore") ? Convert.ToDouble(data["passingScore"] ?? 50) : 50,
                Type = data.ContainsKey("type") ? Enum.Parse<ExamType>(data["type"]?.ToString() ?? "Quiz") : ExamType.Quiz,
                IsPublished = data.ContainsKey("isPublished") ? Convert.ToBoolean(data["isPublished"] ?? false) : false,
                IsActive = data.ContainsKey("isActive") ? Convert.ToBoolean(data["isActive"] ?? false) : false,
                AllowReview = data.ContainsKey("allowReview") ? Convert.ToBoolean(data["allowReview"] ?? true) : true,
                ShowScore = data.ContainsKey("showScore") ? Convert.ToBoolean(data["showScore"] ?? true) : true
            };
        }

        private static ExamSubmission MapToSubmission(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();
            return new ExamSubmission
            {
                Id = doc.Id,
                ExamId = data.ContainsKey("examId") ? data["examId"]?.ToString() ?? "" : "",
                StudentId = data.ContainsKey("studentId") ? data["studentId"]?.ToString() ?? "" : "",
                StudentName = data.ContainsKey("studentName") ? data["studentName"]?.ToString() ?? "" : "",
                Score = data.ContainsKey("score") ? Convert.ToDouble(data["score"] ?? 0) : 0,
                Percentage = data.ContainsKey("percentage") ? Convert.ToDouble(data["percentage"] ?? 0) : 0,
                Status = data.ContainsKey("status") ? Enum.Parse<SubmissionStatus>(data["status"]?.ToString() ?? "Submitted") : SubmissionStatus.Submitted,
                TimeSpentSeconds = data.ContainsKey("timeSpentSeconds") ? Convert.ToInt32(data["timeSpentSeconds"] ?? 0) : 0
            };
        }
    }
}