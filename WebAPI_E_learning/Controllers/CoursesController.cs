using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebAPI_E_learning.Models;

namespace WebAPI_E_learning.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public CoursesController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private Dictionary<string, object> ConvertFirestoreTypes(Dictionary<string, object> dict)
        {
            var newDict = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                newDict[kvp.Key] = ConvertFirestoreTypes(kvp.Value);
            }
            return newDict;
        }

        private object ConvertFirestoreTypes(object value)
        {
            if (value is Timestamp timestamp)
            {
                return timestamp.ToDateTime().ToUniversalTime();
            }
            if (value is Dictionary<string, object> dict)
            {
                return ConvertFirestoreTypes(dict);
            }
            if (value is List<object> list)
            {
                var newList = new List<object>();
                foreach (var item in list)
                {
                    newList.Add(ConvertFirestoreTypes(item));
                }
                return newList;
            }
            return value;
        }

        // ==========================================
        // 1. QUẢN LÝ KHÓA HỌC (COURSES)
        // ==========================================

        [HttpGet]
        [AllowAnonymous] // Anyone can view courses
        public async Task<IActionResult> GetAllCourses()
        {
            var snapshot = await _firestoreDb.Collection("Courses").GetSnapshotAsync();
            var courses = new List<object>();

            foreach (var doc in snapshot.Documents)
            {
                var courseData = doc.ToDictionary();
                string id = doc.Id;

                // Calculate actual StudentCount dynamically
                var regSnap = await _firestoreDb.Collection("courseRegistrations")
                    .WhereEqualTo("courseId", id)
                    .WhereIn("status", new[] { "accepted", "active" })
                    .GetSnapshotAsync();
                int actualStudentCount = regSnap.Documents.Count;

                // Calculate actual AssignmentCount dynamically
                var asmSnap = _firestoreDb.Collection("Courses").Document(id).Collection("Assignments");
                var asmSnapDoc = await asmSnap.GetSnapshotAsync();
                int actualAssignmentCount = asmSnapDoc.Documents.Count;

                courseData["StudentCount"] = actualStudentCount;
                courseData["AssignmentCount"] = actualAssignmentCount;

                courses.Add(new
                {
                    Id = id,
                    Data = ConvertFirestoreTypes(courseData)
                });
            }

            return Ok(courses);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourseDetail(string id)
        {
            var docRef = _firestoreDb.Collection("Courses").Document(id);
            var docSnap = await docRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Course not found." });

            var courseData = docSnap.ToDictionary();

            // Calculate actual StudentCount dynamically
            var regSnap = await _firestoreDb.Collection("courseRegistrations")
                .WhereEqualTo("courseId", id)
                .WhereIn("status", new[] { "accepted", "active" })
                .GetSnapshotAsync();
            int actualStudentCount = regSnap.Documents.Count;

            // Calculate actual AssignmentCount dynamically
            var asmSnap = await docRef.Collection("Assignments").GetSnapshotAsync();
            int actualAssignmentCount = asmSnap.Documents.Count;

            courseData["StudentCount"] = actualStudentCount;
            courseData["AssignmentCount"] = actualAssignmentCount;

            // Fetch lessons from the root Lessons collection to align with GetLessons and AddLesson
            var lessonsSnap = await _firestoreDb.Collection("Lessons")
                .WhereEqualTo("CourseId", id)
                .GetSnapshotAsync();
            var lessons = lessonsSnap.Documents
                .Select(d => new { Id = d.Id, Data = ConvertFirestoreTypes(d.ToDictionary()) })
                .OrderBy(l => l.Data.ContainsKey("CreatedAt") ? l.Data["CreatedAt"] : null)
                .ToList();

            return Ok(new
            {
                Id = docSnap.Id,
                Data = ConvertFirestoreTypes(courseData),
                Lessons = lessons
            });
        }

        [HttpPost]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request)
        {
            var courseData = new Dictionary<string, object>
            {
                { "Title", request.Title },
                { "Description", request.Description },
                { "ThumbnailUrl", request.ThumbnailUrl },
                { "Price", (double)request.Price },
                { "ClassName", request.ClassName },
                { "CourseType", request.CourseType },
                { "Category", request.Category },
                { "DayOfWeek", request.DayOfWeek },
                { "StartPeriod", request.StartPeriod },
                { "EndPeriod", request.EndPeriod },
                { "Semester", request.Semester },
                { "Emoji", request.Emoji },
                { "AccentColor", request.AccentColor },
                { "InstructorId", string.IsNullOrEmpty(request.InstructorId) ? GetCurrentUserId() : request.InstructorId },
                { "CreatedAt", DateTime.UtcNow },
                { "IsActive", request.IsActive },
                { "StudentCount", request.StudentCount },
                { "AssignmentCount", request.AssignmentCount }
            };

            DocumentReference docRef;
            if (!string.IsNullOrEmpty(request.Courseid))
            {
                docRef = _firestoreDb.Collection("Courses").Document(request.Courseid);
                await docRef.SetAsync(courseData);
            }
            else
            {
                docRef = await _firestoreDb.Collection("Courses").AddAsync(courseData);
            }

            return Ok(new { Message = "Course created successfully.", Id = docRef.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateCourse(string id, [FromBody] System.Text.Json.JsonElement requestBody)
        {
            var docRef = _firestoreDb.Collection("Courses").Document(id);
            var docSnap = await docRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Course not found." });

            var allowedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Title", "Title" },
                { "Description", "Description" },
                { "ClassName", "ClassName" },
                { "Category", "Category" },
                { "Semester", "Semester" },
                { "Emoji", "Emoji" },
                { "AccentColor", "AccentColor" },
                { "StudentCount", "StudentCount" },
                { "AssignmentCount", "AssignmentCount" },
                { "IsActive", "IsActive" },
                { "DayOfWeek", "DayOfWeek" },
                { "StartPeriod", "StartPeriod" },
                { "EndPeriod", "EndPeriod" },
                { "Price", "Price" },
                { "ThumbnailUrl", "ThumbnailUrl" },
                { "InstructorId", "InstructorId" },
                { "CourseType", "CourseType" }
            };

            var updates = new Dictionary<string, object>();
            foreach (var property in requestBody.EnumerateObject())
            {
                if (allowedFields.TryGetValue(property.Name, out string firestoreKey))
                {
                    var val = property.Value;
                    switch (val.ValueKind)
                    {
                        case System.Text.Json.JsonValueKind.Null:
                            updates[firestoreKey] = null;
                            break;
                        case System.Text.Json.JsonValueKind.String:
                            updates[firestoreKey] = val.GetString() ?? "";
                            break;
                        case System.Text.Json.JsonValueKind.Number:
                            if (val.TryGetInt32(out int iVal))
                            {
                                updates[firestoreKey] = iVal;
                            }
                            else if (val.TryGetDouble(out double dVal))
                            {
                                updates[firestoreKey] = dVal;
                            }
                            break;
                        case System.Text.Json.JsonValueKind.True:
                            updates[firestoreKey] = true;
                            break;
                        case System.Text.Json.JsonValueKind.False:
                            updates[firestoreKey] = false;
                            break;
                        default:
                            updates[firestoreKey] = val.ToString();
                            break;
                    }
                }
            }

            if (updates.Count > 0)
            {
                updates["UpdatedAt"] = DateTime.UtcNow;
                await docRef.UpdateAsync(updates);
            }

            return Ok(new { Message = "Course updated successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            var docRef = _firestoreDb.Collection("Courses").Document(id);
            if (!(await docRef.GetSnapshotAsync()).Exists) return NotFound();

            // Xóa tất cả các thông báo liên quan đến lớp học này
            var notifsSnap = await _firestoreDb.Collection("Notifications")
                .WhereEqualTo("CourseId", id)
                .GetSnapshotAsync();
            if (notifsSnap.Documents.Count > 0)
            {
                var batch = _firestoreDb.StartBatch();
                foreach (var doc in notifsSnap.Documents)
                {
                    batch.Delete(doc.Reference);
                }
                await batch.CommitAsync();
            }

            await docRef.DeleteAsync();
            return Ok(new { Message = "Course deleted successfully." });
        }

        // ==========================================
        // 2. QUẢN LÝ BÀI GIẢNG (LESSONS)
        // ==========================================

        [HttpGet("{courseId}/lessons")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLessons(string courseId)
        {
            var snap = await _firestoreDb.Collection("Lessons")
                .WhereEqualTo("CourseId", courseId)
                .GetSnapshotAsync();
            
            // Lọc OrderBy ở local để tránh lỗi index missing trên Firebase
            var lessons = snap.Documents
                .Select(d => new { Id = d.Id, Data = ConvertFirestoreTypes(d.ToDictionary()) })
                .OrderBy(l => l.Data.ContainsKey("CreatedAt") ? l.Data["CreatedAt"] : null)
                .ToList();
                
            return Ok(lessons);
        }

        [HttpPost("{courseId}/lessons")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> AddLesson(string courseId, [FromBody] CreateLessonRequest request)
        {
            var courseRef = _firestoreDb.Collection("Courses").Document(courseId);
            if (!(await courseRef.GetSnapshotAsync()).Exists) return NotFound("Course not found.");

            var lessonData = new Dictionary<string, object>
            {
                { "CourseId", courseId },
                { "Title", request.Title },
                { "VideoUrl", request.VideoUrl },
                { "DocumentUrl", request.DocumentUrl },
                { "Description", request.Description },
                { "Order", request.Order },
                { "CreatedAt", DateTime.UtcNow }
            };

            var lessonRef = await _firestoreDb.Collection("Lessons").AddAsync(lessonData);
            
            // Return structured data containing both Id and the Lesson representation
            var lessonResponse = new
            {
                Id = lessonRef.Id,
                Data = new
                {
                    Id = lessonRef.Id,
                    CourseId = courseId,
                    Title = request.Title,
                    VideoUrl = request.VideoUrl,
                    DocumentUrl = request.DocumentUrl,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                }
            };
            return Ok(lessonResponse);
        }

        [HttpPut("{courseId}/lessons/{lessonId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateLesson(string courseId, string lessonId, [FromBody] UpdateLessonRequest request)
        {
            var lessonRef = _firestoreDb.Collection("Lessons").Document(lessonId);
            var lessonSnap = await lessonRef.GetSnapshotAsync();
            if (!lessonSnap.Exists || lessonSnap.GetValue<string>("CourseId") != courseId) return NotFound("Lesson not found.");

            var updates = new Dictionary<string, object>
            {
                { "Title", request.Title },
                { "VideoUrl", request.VideoUrl },
                { "DocumentUrl", request.DocumentUrl },
                { "Description", request.Description },
                { "Order", request.Order },
                { "UpdatedAt", DateTime.UtcNow }
            };

            await lessonRef.UpdateAsync(updates);
            return Ok(new { Message = "Lesson updated successfully." });
        }

        [HttpDelete("{courseId}/lessons/{lessonId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteLesson(string courseId, string lessonId)
        {
            var lessonRef = _firestoreDb.Collection("Lessons").Document(lessonId);
            var lessonSnap = await lessonRef.GetSnapshotAsync();
            if (!lessonSnap.Exists || lessonSnap.GetValue<string>("CourseId") != courseId) return NotFound();

            await lessonRef.DeleteAsync();
            return Ok(new { Message = "Lesson deleted successfully." });
        }

        // ==========================================
        // 3. ĐĂNG KÝ KHÓA HỌC (REGISTRATIONS)
        // ==========================================

        [HttpPost("{courseId}/register")]
        [Authorize]
        public async Task<IActionResult> RegisterCourse(string courseId)
        {
            string uid = GetCurrentUserId();
            var courseRef = _firestoreDb.Collection("Courses").Document(courseId);
            if (!(await courseRef.GetSnapshotAsync()).Exists) return NotFound("Course not found.");

            string regId = $"{uid}_{courseId}";
            var regData = new Dictionary<string, object>
            {
                { "userId", uid },
                { "courseId", courseId },
                { "requestDate", Google.Cloud.Firestore.FieldValue.ServerTimestamp },
                { "status", "pending" },
                { "approvedDate", null },
                { "progressPercentage", 0.0 }
            };

            await _firestoreDb.Collection("courseRegistrations").Document(regId).SetAsync(regData);
            return Ok(new { Message = "Successfully registered for the course." });
        }

        [HttpDelete("{courseId}/register")]
        [Authorize]
        public async Task<IActionResult> CancelRegistration(string courseId)
        {
            string uid = GetCurrentUserId();
            string regId = $"{uid}_{courseId}";
            await _firestoreDb.Collection("courseRegistrations").Document(regId).DeleteAsync();
            return Ok(new { Message = "Registration cancelled." });
        }

        [HttpGet("my-registrations")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> GetMyRegistrations()
        {
            string uid = GetCurrentUserId();
            var snapshot = await _firestoreDb.Collection("courseRegistrations")
                .WhereEqualTo("userId", uid)
                .GetSnapshotAsync();
                
            var registrations = snapshot.Documents.Select(d => new
            {
                Id = d.Id,
                Data = ConvertFirestoreTypes(d.ToDictionary())
            });

            return Ok(registrations);
        }

        [HttpGet("{courseId}/students")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetCourseStudents(string courseId)
        {
            var snapshot = await _firestoreDb.Collection("courseRegistrations")
                .WhereEqualTo("courseId", courseId)
                .GetSnapshotAsync();

            var results = new List<object>();
            foreach (var doc in snapshot.Documents)
            {
                var data = ConvertFirestoreTypes(doc.ToDictionary());
                string userId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? "" : "";
                
                // Join user info
                string fullName = "Học viên";
                string email = "";
                if (!string.IsNullOrEmpty(userId))
                {
                    var userDoc = await _firestoreDb.Collection("Users").Document(userId).GetSnapshotAsync();
                    if (userDoc.Exists)
                    {
                        if (userDoc.TryGetValue("FullName", out string fn) || userDoc.TryGetValue("fullName", out fn)) fullName = fn;
                        if (userDoc.TryGetValue("Email", out string em) || userDoc.TryGetValue("email", out em)) email = em;
                    }
                }
                data["fullName"] = fullName;
                data["email"] = email;

                results.Add(new { Id = doc.Id, Data = data });
            }

            return Ok(results);
        }

        [HttpPut("{courseId}/registrations/{regId}/approve")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> ApproveRegistration(string courseId, string regId)
        {
            var regRef = _firestoreDb.Collection("courseRegistrations").Document(regId);
            if (!(await regRef.GetSnapshotAsync()).Exists) return NotFound("Registration not found.");

            var updates = new Dictionary<string, object>
            {
                { "status", "accepted" },
                { "approvedDate", Google.Cloud.Firestore.FieldValue.ServerTimestamp }
            };
            await regRef.UpdateAsync(updates);
            
            return Ok(new { Message = "Registration approved." });
        }

        [HttpPut("{courseId}/registrations/{regId}/reject")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> RejectRegistration(string courseId, string regId)
        {
            var regRef = _firestoreDb.Collection("courseRegistrations").Document(regId);
            if (!(await regRef.GetSnapshotAsync()).Exists) return NotFound("Registration not found.");

            await regRef.UpdateAsync("status", "rejected");
            return Ok(new { Message = "Registration rejected." });
        }

        [HttpDelete("{courseId}/registrations/{regId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> RemoveRegistration(string courseId, string regId)
        {
            var regRef = _firestoreDb.Collection("courseRegistrations").Document(regId);
            if (!(await regRef.GetSnapshotAsync()).Exists) return NotFound("Registration not found.");

            await regRef.DeleteAsync();
            // Optional: Decrement student count on course here
            return Ok(new { Message = "Registration removed." });
        }

        // ==========================================
        // 4. BÀI TẬP & NỘP BÀI (ASSIGNMENTS & SUBMISSIONS)
        // ==========================================

        [HttpGet("{courseId}/assignments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAssignments(string courseId)
        {
            var asmSnap = await _firestoreDb.Collection("Courses").Document(courseId)
                                            .Collection("Assignments").GetSnapshotAsync();
            var assignments = asmSnap.Documents.Select(d => new { Id = d.Id, Data = ConvertFirestoreTypes(d.ToDictionary()) });
            return Ok(assignments);
        }

        [HttpGet("{courseId}/assignments/{asmId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAssignmentDetail(string courseId, string asmId)
        {
            var docRef = _firestoreDb.Collection("Courses").Document(courseId)
                                     .Collection("Assignments").Document(asmId);
            var docSnap = await docRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Assignment not found." });

            return Ok(new
            {
                Id = docSnap.Id,
                Data = ConvertFirestoreTypes(docSnap.ToDictionary())
            });
        }

        [HttpPost("{courseId}/assignments")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateAssignment(string courseId, [FromBody] CreateAssignmentRequest request)
        {
            var courseRef = _firestoreDb.Collection("Courses").Document(courseId);
            if (!(await courseRef.GetSnapshotAsync()).Exists) return NotFound("Course not found.");

            var asmData = new Dictionary<string, object>
            {
                { "Title", request.Title },
                { "Description", request.Description },
                { "DueDate", request.DueDate.ToUniversalTime() },
                { "AttachedFileUrl", request.AttachedFileUrl ?? "" },
                { "CreatedAt", DateTime.UtcNow }
            };

            var asmRef = await courseRef.Collection("Assignments").AddAsync(asmData);
            return Ok(new { Message = "Assignment created successfully.", Id = asmRef.Id });
        }

        [HttpPost("{courseId}/assignments/{asmId}/submit")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> SubmitAssignment(string courseId, string asmId, [FromBody] SubmitAssignmentRequest request)
        {
            string uid = GetCurrentUserId();
            var asmRef = _firestoreDb.Collection("Courses").Document(courseId).Collection("Assignments").Document(asmId);
            var asmSnap = await asmRef.GetSnapshotAsync();
            if (!asmSnap.Exists) return NotFound("Assignment not found.");

            // Tính toán xem có nộp muộn (trễ hạn) hay không
            bool isLate = false;
            var asmDict = asmSnap.ToDictionary();
            if (asmDict.ContainsKey("DueDate") && asmDict["DueDate"] != null)
            {
                if (asmDict["DueDate"] is Timestamp ts)
                {
                    isLate = DateTime.UtcNow > ts.ToDateTime().ToUniversalTime();
                }
                else if (DateTime.TryParse(asmDict["DueDate"].ToString(), out DateTime dueDate))
                {
                    isLate = DateTime.UtcNow > dueDate.ToUniversalTime();
                }
            }

            var subData = new Dictionary<string, object>
            {
                { "StudentId", uid },
                { "FileUrl", request.FileUrl },
                { "Content", request.Content },
                { "SubmittedAt", DateTime.UtcNow },
                { "IsLate", isLate },
                { "Score", null }
            };

            await asmRef.Collection("Submissions").Document(uid).SetAsync(subData);
            
            return Ok(new { Message = "Assignment submitted successfully." });
        }

        [HttpGet("{courseId}/assignments/{asmId}/submissions")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> GetSubmissions(string courseId, string asmId)
        {
            var submissionsCol = _firestoreDb.Collection("Courses").Document(courseId)
                                            .Collection("Assignments").Document(asmId)
                                            .Collection("Submissions");

            if (User.IsInRole("Student"))
            {
                string uid = GetCurrentUserId();
                var subDoc = await submissionsCol.Document(uid).GetSnapshotAsync();
                if (subDoc.Exists)
                {
                    return Ok(new[] { new { Id = subDoc.Id, Data = ConvertFirestoreTypes(subDoc.ToDictionary()) } });
                }
                return Ok(new object[] { });
            }
            else
            {
                var subSnap = await submissionsCol.GetSnapshotAsync();
                var submissions = subSnap.Documents.Select(d => new { Id = d.Id, Data = ConvertFirestoreTypes(d.ToDictionary()) });
                return Ok(submissions);
            }
        }

        [HttpPut("{courseId}/assignments/{asmId}/submissions/{studentId}/grade")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GradeSubmission(string courseId, string asmId, string studentId, [FromBody] GradeSubmissionRequest request)
        {
            var subRef = _firestoreDb.Collection("Courses").Document(courseId)
                                     .Collection("Assignments").Document(asmId)
                                     .Collection("Submissions").Document(studentId);
            
            if (!(await subRef.GetSnapshotAsync()).Exists) return NotFound("Submission not found.");

            var updates = new Dictionary<string, object>
            {
                { "Score", request.Score },
                { "Comment", request.Comment }
            };

            await subRef.UpdateAsync(updates);
            return Ok(new { Message = "Submission graded successfully." });
        }

        [HttpPut("{courseId}/assignments/{asmId}/publish-grades")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> PublishGrades(string courseId, string asmId)
        {
            var asmRef = _firestoreDb.Collection("Courses").Document(courseId)
                                     .Collection("Assignments").Document(asmId);

            if (!(await asmRef.GetSnapshotAsync()).Exists) return NotFound("Assignment not found.");

            await asmRef.UpdateAsync("IsGradesPublished", true);
            return Ok(new { Message = "Grades published successfully." });
        }
        [HttpPut("{courseId}/assignments/{asmId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateAssignment(string courseId, string asmId, [FromBody] UpdateAssignmentRequest request)
        {
            var asmRef = _firestoreDb.Collection("Courses").Document(courseId).Collection("Assignments").Document(asmId);
            if (!(await asmRef.GetSnapshotAsync()).Exists) return NotFound("Assignment not found.");

            var updates = new Dictionary<string, object>();
            if (request.Title != null) updates.Add("Title", request.Title);
            if (request.Description != null) updates.Add("Description", request.Description);
            if (request.Deadline != default) updates.Add("Deadline", request.Deadline.ToUniversalTime());
            if (request.AttachedFileUrl != null) updates.Add("AttachedFileUrl", request.AttachedFileUrl);
            updates.Add("UpdatedAt", DateTime.UtcNow);

            await asmRef.UpdateAsync(updates);
            return Ok(new { Message = "Assignment updated successfully." });
        }

        [HttpDelete("{courseId}/assignments/{asmId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteAssignment(string courseId, string asmId)
        {
            var asmRef = _firestoreDb.Collection("Courses").Document(courseId).Collection("Assignments").Document(asmId);
            if (!(await asmRef.GetSnapshotAsync()).Exists) return NotFound("Assignment not found.");

            await asmRef.DeleteAsync();
            return Ok(new { Message = "Assignment deleted successfully." });
        }

        // ==========================================
        // 5. NỘI DUNG KHÓA HỌC (COURSE CONTENTS)
        // ==========================================

        [HttpGet("{courseId}/contents")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCourseContents(string courseId)
        {
            var snap = await _firestoreDb.Collection("Courses").Document(courseId)
                                         .Collection("Contents").OrderBy("OrderIndex").GetSnapshotAsync();
            var contents = snap.Documents.Select(d => new { Id = d.Id, Data = ConvertFirestoreTypes(d.ToDictionary()) });
            return Ok(contents);
        }

        [HttpPost("{courseId}/contents")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> AddCourseContent(string courseId, [FromBody] CreateCourseContentRequest request)
        {
            var courseRef = _firestoreDb.Collection("Courses").Document(courseId);
            if (!(await courseRef.GetSnapshotAsync()).Exists) return NotFound("Course not found.");

            var contentData = new Dictionary<string, object>
            {
                { "CourseId", courseId },
                { "Title", request.Title },
                { "Type", request.Type },
                { "Data", request.Data },
                { "OrderIndex", request.OrderIndex },
                { "CreatedAt", DateTime.UtcNow }
            };

            var contentRef = await courseRef.Collection("Contents").AddAsync(contentData);
            return Ok(new { Message = "Content added successfully.", Id = contentRef.Id });
        }

        [HttpPut("{courseId}/contents/{contentId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateCourseContent(string courseId, string contentId, [FromBody] UpdateCourseContentRequest request)
        {
            var contentRef = _firestoreDb.Collection("Courses").Document(courseId).Collection("Contents").Document(contentId);
            if (!(await contentRef.GetSnapshotAsync()).Exists) return NotFound("Content not found.");

            var updates = new Dictionary<string, object>();
            if (request.Title != null) updates.Add("Title", request.Title);
            if (request.Type != null) updates.Add("Type", request.Type);
            if (request.Data != null) updates.Add("Data", request.Data);
            if (request.OrderIndex.HasValue) updates.Add("OrderIndex", request.OrderIndex.Value);

            await contentRef.UpdateAsync(updates);
            return Ok(new { Message = "Content updated successfully." });
        }

        [HttpDelete("{courseId}/contents/{contentId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteCourseContent(string courseId, string contentId)
        {
            var contentRef = _firestoreDb.Collection("Courses").Document(courseId).Collection("Contents").Document(contentId);
            if (!(await contentRef.GetSnapshotAsync()).Exists) return NotFound("Content not found.");

            await contentRef.DeleteAsync();
            return Ok(new { Message = "Content deleted successfully." });
        }
    }
}
