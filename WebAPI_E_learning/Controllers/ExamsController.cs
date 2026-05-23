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
    public class ExamsController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public ExamsController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }



        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private object ConvertFirestoreTypes(object value)
        {
            if (value is Timestamp timestamp)
            {
                return timestamp.ToDateTime();
            }
            if (value is IDictionary<string, object> dict)
            {
                var newDict = new Dictionary<string, object>();
                foreach (var kvp in dict)
                {
                    newDict[kvp.Key] = ConvertFirestoreTypes(kvp.Value);
                }
                return newDict;
            }
            if (value is System.Collections.IList list)
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

        private Dictionary<string, object> CleanExamData(Dictionary<string, object> data)
        {
            data = ConvertFirestoreTypes(data) as Dictionary<string, object> ?? data;

            if (!data.ContainsKey("IsActive"))
            {
                data["IsActive"] = data.ContainsKey("IsPublished") ? data["IsPublished"] : false;
            }

            // Đồng bộ giữa dữ liệu thi cũ (TimeLimitMinutes) và dữ liệu thi mới (DurationMinutes)
            if (data.TryGetValue("TimeLimitMinutes", out var tlm) && !data.ContainsKey("DurationMinutes"))
            {
                data["DurationMinutes"] = tlm;
            }
            else if (data.TryGetValue("DurationMinutes", out var dm) && !data.ContainsKey("TimeLimitMinutes"))
            {
                data["TimeLimitMinutes"] = dm;
            }

            return data;
        }

        private async Task<List<object>> EnrichSubmissions(IEnumerable<DocumentSnapshot> documents)
        {
            var resultList = new List<object>();
            var userNamesCache = new Dictionary<string, string>();

            foreach (var doc in documents)
            {
                var id = doc.Id;
                var data = doc.ToDictionary();
                data = ConvertFirestoreTypes(data) as Dictionary<string, object> ?? data;

                if (!data.ContainsKey("CourseId") || string.IsNullOrEmpty(data["CourseId"]?.ToString()))
                {
                    if (data.TryGetValue("ExamId", out object examIdObj) && examIdObj != null)
                    {
                        var examId = examIdObj.ToString();
                        var examRef = _firestoreDb.Collection("exams").Document(examId);
                        var examSnap = await examRef.GetSnapshotAsync();
                        if (examSnap.Exists)
                        {
                            var examData = examSnap.ToDictionary();
                            if (examData.TryGetValue("ClassId", out object classIdObj))
                            {
                                data["CourseId"] = classIdObj;
                            }
                        }
                    }
                }

                if (!data.ContainsKey("StudentName") || string.IsNullOrEmpty(data["StudentName"]?.ToString()))
                {
                    if (data.TryGetValue("StudentId", out object studentIdObj) && studentIdObj != null)
                    {
                        var studentId = studentIdObj.ToString();
                        if (!userNamesCache.TryGetValue(studentId, out string fullName))
                        {
                            fullName = "Student";
                            try
                            {
                                var userSnap = await _firestoreDb.Collection("Users").Document(studentId).GetSnapshotAsync();
                                if (userSnap.Exists && userSnap.ContainsField("FullName"))
                                {
                                    fullName = userSnap.GetValue<string>("FullName");
                                }
                            }
                            catch { }
                            userNamesCache[studentId] = fullName;
                        }
                        data["StudentName"] = fullName;
                    }
                    else
                    {
                        data["StudentName"] = "Student";
                    }
                }

                // CHUẨN HOÁ Answers: Đôi khi Firestore lưu Answers là một Map/Dictionary thay vì Array
                if (data.TryGetValue("Answers", out var ansObj))
                {
                    var formattedAnswers = new List<object>();
                    if (ansObj is IDictionary<string, object> dictAns)
                    {
                        foreach (var kvp in dictAns)
                        {
                            formattedAnswers.Add(new
                            {
                                QuestionOrder = int.TryParse(kvp.Key, out int idx) ? idx + 1 : 0,
                                StudentAnswer = kvp.Value?.ToString(),
                                IsCorrect = (bool?)null,
                                PointsEarned = 0.0
                            });
                        }
                    }
                    else if (ansObj is IList<object> listAns)
                    {
                        for (int i = 0; i < listAns.Count; i++)
                        {
                            if (listAns[i] is IDictionary<string, object> obj) formattedAnswers.Add(obj);
                            else formattedAnswers.Add(new
                            {
                                QuestionOrder = i + 1,
                                StudentAnswer = listAns[i]?.ToString(),
                                IsCorrect = (bool?)null,
                                PointsEarned = 0.0
                            });
                        }
                    }
                    data["Answers"] = formattedAnswers;
                }
                else
                {
                    data["Answers"] = new List<object>();
                }

                if (!data.ContainsKey("Percentage"))
                {
                    double score = 0;
                    double total = 0;
                    if (data.TryGetValue("Score", out object scoreObj) && scoreObj != null)
                    {
                        score = Convert.ToDouble(scoreObj);
                    }
                    if (data.TryGetValue("TotalQuestions", out object totalObj) && totalObj != null)
                    {
                        total = Convert.ToDouble(totalObj);
                    }
                    data["Percentage"] = total > 0 ? Math.Round((score / total) * 100, 2) : 0.0;
                }

                resultList.Add(new { Id = id, Data = data });
            }

            return resultList;
        }

        [HttpGet("course/{courseId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetExamsForCourse(string courseId)
        {
            var snapshot = await _firestoreDb.Collection("exams")
                                             .WhereEqualTo("ClassId", courseId)
                                             .GetSnapshotAsync();
            var exams = snapshot.Documents.Select(d => new { Id = d.Id, Data = CleanExamData(d.ToDictionary()) });
            return Ok(exams);
        }

        [HttpGet]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetAllExams()
        {
            string uid = GetCurrentUserId();
            var snapshot = await _firestoreDb.Collection("exams")
                                             .WhereEqualTo("InstructorId", uid)
                                             .GetSnapshotAsync();
            var exams = snapshot.Documents.Select(d => new { Id = d.Id, Data = CleanExamData(d.ToDictionary()) });
            return Ok(exams);
        }

        private object ConvertJsonElement(object value)
        {
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        if (jsonElement.TryGetDateTime(out DateTime dateTime))
                        {
                            return dateTime.ToUniversalTime();
                        }
                        return jsonElement.GetString();
                    case System.Text.Json.JsonValueKind.Number:
                        if (jsonElement.TryGetInt64(out long l))
                        {
                            return l;
                        }
                        if (jsonElement.TryGetDouble(out double d))
                        {
                            return d;
                        }
                        return jsonElement.GetDouble();
                    case System.Text.Json.JsonValueKind.True:
                        return true;
                    case System.Text.Json.JsonValueKind.False:
                        return false;
                    case System.Text.Json.JsonValueKind.Null:
                        return null;
                    case System.Text.Json.JsonValueKind.Object:
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in jsonElement.EnumerateObject())
                        {
                            dict[prop.Name] = ConvertJsonElement(prop.Value);
                        }
                        return dict;
                    case System.Text.Json.JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            list.Add(ConvertJsonElement(item));
                        }
                        return list;
                    default:
                        return jsonElement.ToString();
                }
            }
            return value;
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateExam(string id, [FromBody] Dictionary<string, object> updates)
        {
            var examRef = _firestoreDb.Collection("exams").Document(id);

            var cleanedUpdates = new Dictionary<string, object>();
            foreach (var kvp in updates)
            {
                cleanedUpdates[kvp.Key] = ConvertJsonElement(kvp.Value);
            }
            cleanedUpdates["UpdatedAt"] = DateTime.UtcNow;

            await examRef.UpdateAsync(cleanedUpdates);
            return Ok(new { Message = "Exam updated successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteExam(string id)
        {
            var examRef = _firestoreDb.Collection("exams").Document(id);
            await examRef.DeleteAsync();
            return Ok(new { Message = "Exam deleted successfully." });
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetExamDetail(string id)
        {
            var docRef = _firestoreDb.Collection("exams").Document(id);
            var docSnap = await docRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Exam not found." });

            var data = CleanExamData(docSnap.ToDictionary());

            // Tự động load câu hỏi từ subcollection "questions" cho các bài thi cũ
            bool hasQuestions = false;
            if (data.TryGetValue("Questions", out var questionsObj) && questionsObj != null)
            {
                if (questionsObj is System.Collections.IEnumerable enumerable && enumerable.GetEnumerator().MoveNext())
                {
                    hasQuestions = true;
                }
            }

            if (!hasQuestions)
            {
                try
                {
                    var subQuestionsSnap = await docRef.Collection("questions").GetSnapshotAsync();
                    if (subQuestionsSnap.Documents.Count > 0)
                    {
                        var questionsList = new List<Dictionary<string, object>>();
                        var sortedDocs = subQuestionsSnap.Documents
                            .Select(d => new { Doc = d, Order = d.ContainsField("QuestionOrder") ? Convert.ToInt32(d.GetValue<object>("QuestionOrder")) : 0 })
                            .OrderBy(x => x.Order)
                            .Select(x => x.Doc);

                        foreach (var doc in sortedDocs)
                        {
                            var qData = doc.ToDictionary();
                            var qDict = new Dictionary<string, object>();

                            qDict["QuestionText"] = qData.TryGetValue("Content", out var content) ? content?.ToString() ?? "" : "";
                            qDict["Options"] = qData.TryGetValue("Options", out var opts) ? opts : new List<string>();
                            qDict["CorrectOptionIndex"] = qData.TryGetValue("CorrectAnswerIndex", out var corr) ? Convert.ToInt32(corr) : 0;
                            qDict["Points"] = qData.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;

                            questionsList.Add(qDict);
                        }
                        data["Questions"] = questionsList;
                        data["TotalQuestions"] = questionsList.Count;
                        hasQuestions = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading subcollection questions: {ex.Message}");
                }
            }

            // Cách 2: Nếu bài thi lưu bằng QuestionIds và câu hỏi nằm ngoài Collection "questions"
            if (!hasQuestions && data.TryGetValue("QuestionIds", out var qIdsObj))
            {
                if (qIdsObj is System.Collections.IEnumerable qIdsEnum)
                {
                    var questionsList = new List<Dictionary<string, object>>();
                    foreach (var qIdObj in qIdsEnum)
                    {
                        var qId = qIdObj?.ToString();
                        if (string.IsNullOrEmpty(qId)) continue;
                        try
                        {
                            var qDoc = await _firestoreDb.Collection("questions").Document(qId).GetSnapshotAsync();
                            if (qDoc.Exists)
                            {
                                var qData = qDoc.ToDictionary();
                                var qDict = new Dictionary<string, object>();
                                qDict["QuestionId"] = qDoc.Id;
                                
                                // Tương thích cả field Content lẫn QuestionText
                                qDict["QuestionText"] = qData.TryGetValue("Content", out var content) ? content?.ToString() : (qData.TryGetValue("QuestionText", out var qt) ? qt?.ToString() : "");
                                qDict["Options"] = qData.TryGetValue("Options", out var opts) ? opts : new List<string>();
                                qDict["CorrectOptionIndex"] = qData.TryGetValue("CorrectAnswerIndex", out var corr) ? Convert.ToInt32(corr) : (qData.TryGetValue("CorrectOptionIndex", out var coi) ? Convert.ToInt32(coi) : 0);
                                qDict["Points"] = qData.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;

                                questionsList.Add(qDict);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading question {qId}: {ex.Message}");
                        }
                    }
                    if (questionsList.Count > 0)
                    {
                        data["Questions"] = questionsList;
                        data["TotalQuestions"] = questionsList.Count;
                        hasQuestions = true;
                    }
                }
            }

            return Ok(new { Id = docSnap.Id, Data = data });
        }

        [HttpPost]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
        {
            string className = "";
            try
            {
                var courseSnap = await _firestoreDb.Collection("Courses").Document(request.CourseId).GetSnapshotAsync();
                if (courseSnap.Exists)
                {
                    if (courseSnap.ContainsField("ClassName"))
                    {
                        className = courseSnap.GetValue<string>("ClassName");
                    }
                    else if (courseSnap.ContainsField("Title"))
                    {
                        className = courseSnap.GetValue<string>("Title");
                    }
                }
            }
            catch { }

            var examData = new Dictionary<string, object>
            {
                { "ClassId", request.CourseId },
                { "ClassName", className },
                { "Title", request.Title },
                { "DurationMinutes", request.DurationMinutes },
                { "TimeLimitMinutes", request.DurationMinutes }, // Thêm để tương thích ngược
                { "Description", request.Description },
                { "PassingScore", request.PassingScore },
                { "IsPublished", request.IsPublished },
                { "IsActive", request.IsActive },
                { "AllowReview", request.AllowReview },
                { "RandomizeQuestions", request.RandomizeQuestions },
                { "ShowScore", request.ShowScore },
                { "AllowMultipleAttempts", request.AllowMultipleAttempts },
                { "MaxAttempts", request.MaxAttempts },
                { "TotalQuestions", request.Questions.Count },
                { "CreatedAt", DateTime.UtcNow },
                { "UpdatedAt", DateTime.UtcNow },
                { "InstructorId", GetCurrentUserId() },
                { "SubjectCode", (object)null }, // Thêm để tương thích ngược
                { "ScheduledDate", (object)null }, // Thêm để tương thích ngược
                { "Deadline", (object)null } // Thêm để tương thích ngược
            };

            var questionsList = new List<Dictionary<string, object>>();
            var questionIds = new List<string>();
            foreach (var q in request.Questions)
            {
                var qId = Guid.NewGuid().ToString("N");
                questionIds.Add(qId);

                questionsList.Add(new Dictionary<string, object>
                {
                    { "QuestionId", qId },
                    { "QuestionText", q.QuestionText },
                    { "Options", q.Options },
                    { "CorrectOptionIndex", q.CorrectOptionIndex },
                    { "Points", q.Points }
                });
            }
            examData.Add("Questions", questionsList);
            examData.Add("QuestionIds", questionIds); // Thêm để tương thích ngược

            var docRef = await _firestoreDb.Collection("exams").AddAsync(examData);
            return Ok(new { Message = "Exam created successfully.", Id = docRef.Id });
        }

        [HttpPost("{id}/submit")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> SubmitExam(string id, [FromBody] SubmitExamRequest request)
        {
            string uid = GetCurrentUserId();
            var examRef = _firestoreDb.Collection("exams").Document(id);
            var examSnap = await examRef.GetSnapshotAsync();

            if (!examSnap.Exists) return NotFound(new { Message = "Exam not found." });

            var examData = examSnap.ToDictionary();

            int score = 0;
            int totalQuestions = 0;

            if (examData.TryGetValue("Questions", out object questionsObj))
            {
                if (questionsObj is List<object> questions)
                {
                    totalQuestions = questions.Count;
                    for (int i = 0; i < questions.Count; i++)
                    {
                        if (questions[i] is Dictionary<string, object> qDict)
                        {
                            if (request.Answers.TryGetValue(i.ToString(), out int studentAnswer))
                            {
                                if (qDict.TryGetValue("CorrectOptionIndex", out object correctOpt))
                                {
                                    // Chuyển đổi an toàn sang int đề phòng Firestore lưu dạng long
                                    if (Convert.ToInt32(correctOpt) == studentAnswer)
                                    {
                                        score++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string studentName = "Student";
            try
            {
                var userSnap = await _firestoreDb.Collection("Users").Document(uid).GetSnapshotAsync();
                if (userSnap.Exists && userSnap.ContainsField("FullName"))
                {
                    studentName = userSnap.GetValue<string>("FullName");
                }
            }
            catch { }

            double percentage = totalQuestions > 0 ? Math.Round(((double)score / totalQuestions) * 100, 2) : 0.0;

            var subData = new Dictionary<string, object>
            {
                { "StudentId", uid },
                { "StudentName", studentName },
                { "ExamId", id },
                { "CourseId", examData.ContainsKey("ClassId") ? examData["ClassId"] : "" },
                { "Score", score },
                { "TotalQuestions", totalQuestions },
                { "Percentage", percentage },
                { "Answers", request.Answers },
                { "SubmittedAt", DateTime.UtcNow }
            };

            await _firestoreDb.Collection("exam_submissions").AddAsync(subData);

            return Ok(new
            {
                Message = "Exam submitted successfully.",
                Score = score,
                TotalQuestions = totalQuestions,
                Percentage = percentage,
                StudentName = studentName,
                SubmittedAt = DateTime.UtcNow
            });
        }

        [HttpGet("{id}/submissions")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetExamSubmissions(string id)
        {
            var snapshot = await _firestoreDb.Collection("exam_submissions")
                                             .WhereEqualTo("ExamId", id)
                                             .GetSnapshotAsync();
            var submissions = await EnrichSubmissions(snapshot.Documents);
            return Ok(submissions);
        }

        [HttpGet("my-history")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> GetMyHistory()
        {
            string uid = GetCurrentUserId();
            var snapshot = await _firestoreDb.Collection("exam_submissions")
                                             .WhereEqualTo("StudentId", uid)
                                             .GetSnapshotAsync();
            var submissions = await EnrichSubmissions(snapshot.Documents);
            return Ok(submissions);
        }

        [HttpGet("my-exams")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> GetMyExams()
        {
            string uid = GetCurrentUserId(); // Đồng bộ cách lấy UID
            Console.WriteLine($"[DEBUG-EXAM] GetMyExams called. UID: {uid}");
            var regSnap = await _firestoreDb.Collection("courseRegistrations")
                .WhereEqualTo("userId", uid)
                .WhereEqualTo("status", "accepted")
                .GetSnapshotAsync();

            var courseIds = regSnap.Documents.Select(d => d.GetValue<string>("courseId")).ToList();
            Console.WriteLine($"[DEBUG-EXAM] Accepted course count: {courseIds.Count}. Course IDs: {string.Join(", ", courseIds)}");
            if (courseIds.Count == 0) return Ok(new List<object>());

            var exams = new List<object>();

            // Chunk ra mỗi lần 10 items do giới hạn của Firestore WhereIn
            for (int i = 0; i < courseIds.Count; i += 10)
            {
                var chunk = courseIds.Skip(i).Take(10).ToList();

                // ĐÃ SỬA: Dùng ClassId để tương thích với dữ liệu cũ
                var examSnap = await _firestoreDb.Collection("exams")
                    .WhereIn("ClassId", chunk)
                    .GetSnapshotAsync();
                Console.WriteLine($"[DEBUG-EXAM] Found {examSnap.Documents.Count} exams matching ClassId in Firestore.");

                // Lọc IsPublished bằng LINQ ở local để code chạy mượt mà
                var filteredExams = examSnap.Documents
                    .Where(d => d.ContainsField("IsPublished") && d.GetValue<bool>("IsPublished") == true)
                    .Select(d => new { Id = d.Id, Data = CleanExamData(d.ToDictionary()) })
                    .ToList();
                Console.WriteLine($"[DEBUG-EXAM] After IsPublished filter, {filteredExams.Count} exams remain.");

                exams.AddRange(filteredExams);
            }
            return Ok(exams);
        }
        // --- DRAFTS ---

        [HttpGet("{examId}/drafts/{studentId}")]
        public async Task<IActionResult> GetExamDraft(string examId, string studentId)
        {
            try
            {
                var draftRef = _firestoreDb.Collection("exams").Document(examId).Collection("drafts").Document(studentId);
                var snapshot = await draftRef.GetSnapshotAsync();
                if (!snapshot.Exists) return Ok(null);
                return Ok(snapshot.ConvertTo<ExamDraft>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{examId}/drafts/{studentId}")]
        public async Task<IActionResult> DeleteExamDraft(string examId, string studentId)
        {
            try
            {
                var draftRef = _firestoreDb.Collection("exams").Document(examId).Collection("drafts").Document(studentId);
                await draftRef.DeleteAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("drafts")]
        public async Task<IActionResult> SaveExamDraft([FromBody] ExamDraft draft)
        {
            try
            {
                if (string.IsNullOrEmpty(draft.ExamId) || string.IsNullOrEmpty(draft.StudentId))
                    return BadRequest("ExamId and StudentId are required");

                var draftRef = _firestoreDb.Collection("exams").Document(draft.ExamId).Collection("drafts").Document(draft.StudentId);
                await draftRef.SetAsync(draft);
                return Ok(draft);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}