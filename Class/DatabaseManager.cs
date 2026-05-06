using e_learning_app;
using Firebase.Auth;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace e_learning_app
{
    public class DatabaseManager
    {
        private FirestoreDb _db;
        private User _currentUser = null;

        public FirestoreDb GetDb => _db;

        public User GetCurrentUser() => _currentUser;
        public void SetCurrentUser(User user) => _currentUser = user;

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
                MessageBox.Show("Firestore chưa được khởi tạo. Hãy đảm bảo FirebaseService.Initialize() đã được gọi trước.",
                                "Lỗi Kết Nối", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}