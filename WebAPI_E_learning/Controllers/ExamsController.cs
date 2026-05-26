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
            var examCache = new Dictionary<string, Dictionary<string, object>>(); // Cache exams to avoid multiple Firestore hits

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
                        if (!examCache.TryGetValue(examId, out var examData))
                        {
                            var examRef = _firestoreDb.Collection("exams").Document(examId);
                            var examSnap = await examRef.GetSnapshotAsync();
                            if (examSnap.Exists)
                            {
                                examData = examSnap.ToDictionary();
                                examCache[examId] = examData;
                            }
                        }
                        if (examData != null && examData.TryGetValue("ClassId", out object classIdObj))
                        {
                            data["CourseId"] = classIdObj;
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

                // Cache exam data if we haven't already for evaluating answers
                string submissionExamId = data.TryGetValue("ExamId", out var eid) ? eid?.ToString() : null;
                Dictionary<string, object> subExamData = null;
                if (!string.IsNullOrEmpty(submissionExamId))
                {
                    if (!examCache.TryGetValue(submissionExamId, out subExamData))
                    {
                        var examSnap = await _firestoreDb.Collection("exams").Document(submissionExamId).GetSnapshotAsync();
                        if (examSnap.Exists)
                        {
                            subExamData = examSnap.ToDictionary();
                            examCache[submissionExamId] = subExamData;
                        }
                    }
                }

                System.Collections.IList questionsList = null;
                if (subExamData != null && subExamData.TryGetValue("Questions", out var qObj) && qObj is System.Collections.IList qList)
                {
                    questionsList = qList;
                }

                // CHUẨN HOÁ Answers: Đôi khi Firestore lưu Answers là một Map/Dictionary thay vì Array
                if (data.TryGetValue("Answers", out var ansObj))
                {
                    var formattedAnswers = new List<object>();
                    if (ansObj is IDictionary<string, object> dictAns)
                    {
                        foreach (var kvp in dictAns)
                        {
                            string qId = kvp.Key;
                            int.TryParse(qId, out int qIdx);
                            string studentAnswer = kvp.Value?.ToString();

                            bool? isCorrect = null;
                            double pointsEarned = 0.0;
                            double questionPoints = 1.0;

                            if (questionsList != null && qIdx >= 0 && qIdx < questionsList.Count)
                            {
                                if (questionsList[qIdx] is Dictionary<string, object> qDict)
                                {
                                    questionPoints = qDict.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                                    if (qDict.TryGetValue("CorrectOptionIndex", out var correctOpt) && int.TryParse(studentAnswer, out int studentOpt))
                                    {
                                        isCorrect = (Convert.ToInt32(correctOpt) == studentOpt);
                                        pointsEarned = isCorrect == true ? questionPoints : 0.0;
                                    }
                                }
                            }

                            formattedAnswers.Add(new
                            {
                                QuestionId = qId,
                                QuestionOrder = qIdx + 1,
                                StudentAnswer = studentAnswer,
                                IsCorrect = isCorrect,
                                PointsEarned = pointsEarned
                            });
                        }
                    }
                    else if (ansObj is System.Collections.IList listAns)
                    {
                        for (int i = 0; i < listAns.Count; i++)
                        {
                            if (listAns[i] is IDictionary<string, object> obj)
                            {
                                var enrichedObj = new Dictionary<string, object>(obj);
                                string qId = enrichedObj.TryGetValue("QuestionId", out var qidVal) ? qidVal?.ToString() : i.ToString();
                                int qIdx = i;
                                if (int.TryParse(qId, out int parsedIdx))
                                {
                                    qIdx = parsedIdx;
                                }
                                string studentAnswer = enrichedObj.TryGetValue("StudentAnswer", out var saVal) ? saVal?.ToString() : null;

                                enrichedObj["QuestionId"] = qId;
                                if (!enrichedObj.ContainsKey("QuestionOrder"))
                                {
                                    enrichedObj["QuestionOrder"] = qIdx + 1;
                                }

                                if (!enrichedObj.ContainsKey("IsCorrect") || enrichedObj["IsCorrect"] == null)
                                {
                                    bool? isCorrect = null;
                                    double pointsEarned = 0.0;
                                    double questionPoints = 1.0;

                                    if (questionsList != null && qIdx >= 0 && qIdx < questionsList.Count)
                                    {
                                        if (questionsList[qIdx] is Dictionary<string, object> qDict)
                                        {
                                            questionPoints = qDict.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                                            if (qDict.TryGetValue("CorrectOptionIndex", out var correctOpt) && int.TryParse(studentAnswer, out int studentOpt))
                                            {
                                                isCorrect = (Convert.ToInt32(correctOpt) == studentOpt);
                                                pointsEarned = isCorrect == true ? questionPoints : 0.0;
                                            }
                                        }
                                    }
                                    enrichedObj["IsCorrect"] = isCorrect;
                                    enrichedObj["PointsEarned"] = pointsEarned;
                                }
                                formattedAnswers.Add(enrichedObj);
                            }
                            else
                            {
                                string qId = i.ToString();
                                string studentAnswer = listAns[i]?.ToString();

                                bool? isCorrect = null;
                                double pointsEarned = 0.0;
                                double questionPoints = 1.0;

                                if (questionsList != null && i < questionsList.Count)
                                {
                                    if (questionsList[i] is Dictionary<string, object> qDict)
                                    {
                                        questionPoints = qDict.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                                        if (qDict.TryGetValue("CorrectOptionIndex", out var correctOpt) && int.TryParse(studentAnswer, out int studentOpt))
                                        {
                                            isCorrect = (Convert.ToInt32(correctOpt) == studentOpt);
                                            pointsEarned = isCorrect == true ? questionPoints : 0.0;
                                        }
                                    }
                                }

                                formattedAnswers.Add(new
                                {
                                    QuestionId = qId,
                                    QuestionOrder = i + 1,
                                    StudentAnswer = studentAnswer,
                                    IsCorrect = isCorrect,
                                    PointsEarned = pointsEarned
                                });
                            }
                        }
                    }
                    data["Answers"] = formattedAnswers;
                }
                else
                {
                    data["Answers"] = new List<object>();
                }

                double percentage = 0.0;
                if (data.TryGetValue("Percentage", out object pctObj) && pctObj != null)
                {
                    percentage = Convert.ToDouble(pctObj);
                }
                else
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
                    percentage = total > 0 ? Math.Round((score / total) * 100, 2) : 0.0;
                    data["Percentage"] = percentage;
                }

                // Đồng bộ và chuẩn hoá Score về hệ điểm 10 dựa trên Percentage
                data["Score"] = Math.Round(percentage / 10.0, 2);

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

            double earnedPoints = 0;
            double totalPoints = 0;
            int correctCount = 0;
            int totalQuestions = 0;

            var richAnswers = new List<Dictionary<string, object>>();

            if (examData.TryGetValue("Questions", out object questionsObj))
            {
                if (questionsObj is System.Collections.IList questions)
                {
                    totalQuestions = questions.Count;
                    for (int i = 0; i < questions.Count; i++)
                    {
                        if (questions[i] is Dictionary<string, object> qDict)
                        {
                            string qId = i.ToString(); // Sử dụng index làm QuestionId để khớp chính xác với Client WPF
                            double questionPoints = qDict.TryGetValue("Points", out var ptsObj) ? Convert.ToDouble(ptsObj) : 1.0;
                            totalPoints += questionPoints;

                            bool isCorrect = false;
                            double pointsEarned = 0.0;
                            string studentAnswer = null;

                            if (request.Answers.TryGetValue(i.ToString(), out int studentOpt))
                            {
                                studentAnswer = studentOpt.ToString();
                                if (qDict.TryGetValue("CorrectOptionIndex", out object correctOpt))
                                {
                                    if (Convert.ToInt32(correctOpt) == studentOpt)
                                    {
                                        correctCount++;
                                        earnedPoints += questionPoints;
                                        isCorrect = true;
                                        pointsEarned = questionPoints;
                                    }
                                }
                            }

                            richAnswers.Add(new Dictionary<string, object>
                            {
                                { "QuestionId", qId },
                                { "QuestionOrder", i + 1 },
                                { "StudentAnswer", studentAnswer },
                                { "IsCorrect", isCorrect },
                                { "PointsEarned", pointsEarned }
                            });
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

            if (totalPoints == 0) totalPoints = totalQuestions > 0 ? totalQuestions : 1.0;
            double percentage = Math.Round((earnedPoints / totalPoints) * 100, 2);
            double finalScore = Math.Round(percentage / 10.0, 2);

            var subData = new Dictionary<string, object>
            {
                { "StudentId", uid },
                { "StudentName", studentName },
                { "ExamId", id },
                { "CourseId", examData.ContainsKey("ClassId") ? examData["ClassId"] : "" },
                { "Score", finalScore }, // Lưu điểm số quy đổi hệ 10 vào DB
                { "TotalQuestions", totalQuestions },
                { "Percentage", percentage },
                { "Answers", richAnswers }, // Lưu câu trả lời chuẩn hoá hoàn chỉnh
                { "SubmittedAt", DateTime.UtcNow }
            };

            await _firestoreDb.Collection("exam_submissions").AddAsync(subData);

            return Ok(new
            {
                Message = "Exam submitted successfully.",
                Score = correctCount, // Trả về số câu đúng để Client hiển thị thông báo tức thời
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

        [HttpGet("{id}/questions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetExamQuestions(string id)
        {
            var docRef = _firestoreDb.Collection("exams").Document(id);
            var docSnap = await docRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Exam not found." });

            var data = docSnap.ToDictionary();
            var questionsList = new List<Dictionary<string, object>>();
            bool hasQuestions = false;

            // 1. Tự động load câu hỏi từ subcollection "questions"
            try
            {
                var subQuestionsSnap = await docRef.Collection("questions").GetSnapshotAsync();
                if (subQuestionsSnap.Documents.Count > 0)
                {
                    var sortedDocs = subQuestionsSnap.Documents
                        .Select(d => new { Doc = d, Order = d.ContainsField("QuestionOrder") ? Convert.ToInt32(d.GetValue<object>("QuestionOrder")) : 0 })
                        .OrderBy(x => x.Order)
                        .Select(x => x.Doc);

                    foreach (var doc in sortedDocs)
                    {
                        var qData = doc.ToDictionary();
                        var qDict = new Dictionary<string, object>();

                        qDict["Id"] = doc.Id;
                        qDict["QuestionOrder"] = qData.TryGetValue("QuestionOrder", out var order) ? Convert.ToInt32(order) : 0;
                        qDict["Type"] = qData.TryGetValue("Type", out var type) ? type?.ToString() : "MultipleChoice";
                        
                        string questionText = qData.TryGetValue("Content", out var content) ? content?.ToString() : (qData.TryGetValue("QuestionText", out var qt) ? qt?.ToString() : "");
                        qDict["Content"] = questionText;
                        qDict["QuestionText"] = questionText;

                        qDict["Options"] = qData.TryGetValue("Options", out var opts) ? opts : new List<string>();

                        int correctIndex = qData.TryGetValue("CorrectAnswerIndex", out var corr) ? Convert.ToInt32(corr) : (qData.TryGetValue("CorrectOptionIndex", out var coi) ? Convert.ToInt32(coi) : 0);
                        qDict["CorrectAnswerIndex"] = correctIndex;
                        qDict["CorrectOptionIndex"] = correctIndex;

                        qDict["Points"] = qData.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                        qDict["MaxWords"] = qData.TryGetValue("MaxWords", out var mw) ? Convert.ToInt32(mw) : 0;

                        questionsList.Add(qDict);
                    }
                    hasQuestions = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading subcollection questions in GetExamQuestions: {ex.Message}");
            }

            // 2. Load từ array "Questions" trong exam document
            if (!hasQuestions && data.TryGetValue("Questions", out var questionsObj) && questionsObj != null)
            {
                if (questionsObj is System.Collections.IEnumerable enumerable)
                {
                    int index = 1;
                    foreach (var qObj in enumerable)
                    {
                        if (qObj is Dictionary<string, object> qData)
                        {
                            var qDict = new Dictionary<string, object>();
                            
                            qDict["Id"] = qData.TryGetValue("QuestionId", out var qid) ? qid?.ToString() : Guid.NewGuid().ToString("N");
                            qDict["QuestionOrder"] = qData.TryGetValue("QuestionOrder", out var order) ? Convert.ToInt32(order) : index++;
                            qDict["Type"] = qData.TryGetValue("Type", out var type) ? type?.ToString() : "MultipleChoice";

                            string questionText = qData.TryGetValue("Content", out var content) ? content?.ToString() : (qData.TryGetValue("QuestionText", out var qt) ? qt?.ToString() : "");
                            qDict["Content"] = questionText;
                            qDict["QuestionText"] = questionText;

                            qDict["Options"] = qData.TryGetValue("Options", out var opts) ? opts : new List<string>();

                            int correctIndex = qData.TryGetValue("CorrectAnswerIndex", out var corr) ? Convert.ToInt32(corr) : (qData.TryGetValue("CorrectOptionIndex", out var coi) ? Convert.ToInt32(coi) : 0);
                            qDict["CorrectAnswerIndex"] = correctIndex;
                            qDict["CorrectOptionIndex"] = correctIndex;

                            qDict["Points"] = qData.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                            qDict["MaxWords"] = qData.TryGetValue("MaxWords", out var mw) ? Convert.ToInt32(mw) : 0;

                            questionsList.Add(qDict);
                        }
                    }
                    hasQuestions = true;
                }
            }

            // 3. Nếu bài thi lưu bằng QuestionIds và câu hỏi nằm ngoài Collection "questions"
            if (!hasQuestions && data.TryGetValue("QuestionIds", out var qIdsObj))
            {
                if (qIdsObj is System.Collections.IEnumerable qIdsEnum)
                {
                    int index = 1;
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
                                
                                qDict["Id"] = qDoc.Id;
                                qDict["QuestionOrder"] = qData.TryGetValue("QuestionOrder", out var order) ? Convert.ToInt32(order) : index++;
                                qDict["Type"] = qData.TryGetValue("Type", out var type) ? type?.ToString() : "MultipleChoice";

                                string questionText = qData.TryGetValue("Content", out var content) ? content?.ToString() : (qData.TryGetValue("QuestionText", out var qt) ? qt?.ToString() : "");
                                qDict["Content"] = questionText;
                                qDict["QuestionText"] = questionText;

                                qDict["Options"] = qData.TryGetValue("Options", out var opts) ? opts : new List<string>();

                                int correctIndex = qData.TryGetValue("CorrectAnswerIndex", out var corr) ? Convert.ToInt32(corr) : (qData.TryGetValue("CorrectOptionIndex", out var coi) ? Convert.ToInt32(coi) : 0);
                                qDict["CorrectAnswerIndex"] = correctIndex;
                                qDict["CorrectOptionIndex"] = correctIndex;

                                qDict["Points"] = qData.TryGetValue("Points", out var pts) ? Convert.ToDouble(pts) : 1.0;
                                qDict["MaxWords"] = qData.TryGetValue("MaxWords", out var mw) ? Convert.ToInt32(mw) : 0;

                                questionsList.Add(qDict);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading question {qId} in GetExamQuestions: {ex.Message}");
                        }
                    }
                }
            }

            return Ok(questionsList);
        }

        [HttpDelete("{id}/questions/{questionId}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteExamQuestion(string id, string questionId)
        {
            var examRef = _firestoreDb.Collection("exams").Document(id);
            var docSnap = await examRef.GetSnapshotAsync();
            if (!docSnap.Exists) return NotFound(new { Message = "Exam not found." });

            var data = docSnap.ToDictionary();
            bool deleted = false;

            // Xóa trong array "Questions" của exam document
            if (data.TryGetValue("Questions", out var questionsObj) && questionsObj is System.Collections.IList questionsList)
            {
                var newQuestions = new List<object>();
                foreach (var qObj in questionsList)
                {
                    if (qObj is Dictionary<string, object> qData)
                    {
                        string qId = qData.TryGetValue("QuestionId", out var qidVal) ? qidVal?.ToString() : "";
                        if (qId == questionId)
                        {
                            deleted = true;
                            continue;
                        }
                    }
                    newQuestions.Add(qObj);
                }

                if (deleted)
                {
                    var updates = new Dictionary<string, object>
                    {
                        { "Questions", newQuestions },
                        { "TotalQuestions", newQuestions.Count },
                        { "UpdatedAt", DateTime.UtcNow }
                    };

                    if (data.TryGetValue("QuestionIds", out var qIdsObj) && qIdsObj is System.Collections.IList qIdsList)
                    {
                        var newQIds = new List<string>();
                        foreach (var qId in qIdsList)
                        {
                            if (qId?.ToString() != questionId)
                            {
                                newQIds.Add(qId?.ToString());
                            }
                        }
                        updates["QuestionIds"] = newQIds;
                    }

                    await examRef.UpdateAsync(updates);
                }
            }

            // Xóa trong subcollection "questions"
            try
            {
                var subDocRef = examRef.Collection("questions").Document(questionId);
                var subDocSnap = await subDocRef.GetSnapshotAsync();
                if (subDocSnap.Exists)
                {
                    await subDocRef.DeleteAsync();
                    deleted = true;

                    var subQuestionsSnap = await examRef.Collection("questions").GetSnapshotAsync();
                    await examRef.UpdateAsync(new Dictionary<string, object>
                    {
                        { "TotalQuestions", subQuestionsSnap.Documents.Count },
                        { "UpdatedAt", DateTime.UtcNow }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting subcollection question in DeleteExamQuestion: {ex.Message}");
            }

            if (!deleted)
            {
                return NotFound(new { Message = "Question not found in the exam." });
            }

            return Ok(new { success = true, Message = "Question deleted successfully." });
        }

        public class SaveExamWithQuestionsRequest
        {
            public string ExamId { get; set; } = string.Empty;
            public string ClassId { get; set; } = string.Empty;
            public string? ClassName { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public int TimeLimitMinutes { get; set; }
            public double PassingScore { get; set; }
            public bool IsPublished { get; set; }
            public bool IsActive { get; set; }
            public bool AllowReview { get; set; }
            public bool RandomizeQuestions { get; set; }
            public bool ShowScore { get; set; }
            public bool AllowMultipleAttempts { get; set; }
            public int MaxAttempts { get; set; }
            public List<QuestionModel> Questions { get; set; } = new();
        }

        [HttpPost("with-questions")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> SaveExamWithQuestions([FromBody] SaveExamWithQuestionsRequest request)
        {
            if (string.IsNullOrEmpty(request.ExamId)) return BadRequest("ExamId is required.");

            var examRef = _firestoreDb.Collection("exams").Document(request.ExamId);
            var docSnap = await examRef.GetSnapshotAsync();

            string className = request.ClassName ?? "";
            if (string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(request.ClassId))
            {
                try
                {
                    var courseSnap = await _firestoreDb.Collection("Courses").Document(request.ClassId).GetSnapshotAsync();
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
            }

            var examData = new Dictionary<string, object>
            {
                { "ClassId", request.ClassId },
                { "ClassName", className },
                { "Title", request.Title ?? "" },
                { "DurationMinutes", request.TimeLimitMinutes },
                { "TimeLimitMinutes", request.TimeLimitMinutes },
                { "Description", request.Description ?? "" },
                { "PassingScore", request.PassingScore },
                { "IsPublished", request.IsPublished },
                { "IsActive", request.IsActive },
                { "AllowReview", request.AllowReview },
                { "RandomizeQuestions", request.RandomizeQuestions },
                { "ShowScore", request.ShowScore },
                { "AllowMultipleAttempts", request.AllowMultipleAttempts },
                { "MaxAttempts", request.MaxAttempts },
                { "TotalQuestions", request.Questions.Count },
                { "UpdatedAt", DateTime.UtcNow }
            };

            var questionsList = new List<Dictionary<string, object>>();
            var questionIds = new List<string>();
            foreach (var q in request.Questions)
            {
                string qId = string.IsNullOrEmpty(q.QuestionId) ? Guid.NewGuid().ToString("N") : q.QuestionId;
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
            examData.Add("QuestionIds", questionIds);

            if (docSnap.Exists)
            {
                await examRef.UpdateAsync(examData);
            }
            else
            {
                examData.Add("CreatedAt", DateTime.UtcNow);
                examData.Add("InstructorId", GetCurrentUserId());
                await examRef.SetAsync(examData);
            }

            return Ok(new { success = true, Message = "Exam and questions saved successfully." });
        }
    }
}