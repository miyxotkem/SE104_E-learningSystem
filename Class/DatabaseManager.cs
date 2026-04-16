    using e_learning_app;
using Firebase.Auth;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using static Google.Cloud.Firestore.V1.StructuredQuery.Types;
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
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase", "firebase_json.json");
            if (File.Exists(path))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            }
            if (_db != null) return;

            string jsonPath = path;
            if (!File.Exists(jsonPath))
            {
                MessageBox.Show($"CẢNH BÁO: Không tìm thấy file JSON tại:\n{jsonPath}\n\nHãy kiểm tra lại đường dẫn!",
                                "Lỗi Đường Dẫn File", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _db = FirestoreDb.Create("e-learning-cd1b3");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khởi tạo Firestore: " + ex.Message);
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
                System.Diagnostics.Debug.WriteLine($"Delete Content Error: {ex.Message}");
                return false;
            }
        }
    }
}