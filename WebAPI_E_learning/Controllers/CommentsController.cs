using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using WebAPI_E_learning.Models;
using System.Security.Claims;
using System;
using System.Collections.Generic;

namespace WebAPI_E_learning.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentsController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public CommentsController(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        private string GetCurrentUserId()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity != null)
            {
                var idClaim = identity.FindFirst("user_id") ?? identity.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim != null) return idClaim.Value;
            }
            return "";
        }

        [HttpGet("lesson/{lessonId}")]
        [Authorize]
        public async Task<IActionResult> GetCommentsByLesson(string lessonId)
        {
            var snap = await _firestoreDb.Collection("Comments")
                .WhereEqualTo("LessonId", lessonId)
                .GetSnapshotAsync();

            var comments = snap.Documents.Select(d => {
                var dict = d.ToDictionary();
                if (dict.TryGetValue("CreatedAt", out var createdAtObj) && createdAtObj is Google.Cloud.Firestore.Timestamp ts)
                {
                    dict["CreatedAt"] = ts.ToDateTime().ToString("o");
                }
                return new
                {
                    Id = d.Id,
                    Data = dict
                };
            })
            .OrderByDescending(c => c.Data.ContainsKey("CreatedAt") ? c.Data["CreatedAt"].ToString() : "");

            return Ok(comments);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            try
            {
                string uid = GetCurrentUserId();
                var docRef = _firestoreDb.Collection("Comments").Document();
                
                var commentDict = new Dictionary<string, object>
                {
                    { "LessonId", request?.LessonId ?? "" },
                    { "ParentId", request?.ParentId ?? "" },
                    { "Content", request?.Content ?? "" },
                    { "UserId", uid },
                    { "UserName", request?.UserName ?? "" },
                    { "UserRole", request?.UserRole ?? "" },
                    { "ProfileImageUrl", request?.ProfileImageUrl ?? "" },
                    { "CreatedAt", Google.Cloud.Firestore.Timestamp.GetCurrentTimestamp() }
                };

                await docRef.SetAsync(commentDict);
                return Ok(new { Id = docRef.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        [HttpDelete("{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(string commentId)
        {
            try
            {
                string uid = GetCurrentUserId();
                var identity = HttpContext.User.Identity as System.Security.Claims.ClaimsIdentity;
                string role = identity?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

                var docRef = _firestoreDb.Collection("Comments").Document(commentId);
                var docSnap = await docRef.GetSnapshotAsync();
                
                if (!docSnap.Exists) return NotFound("Bình luận không tồn tại");
                
                string authorId = docSnap.GetValue<string>("UserId");
                
                if (role == "Instructor" || uid == authorId)
                {
                    await docRef.DeleteAsync();
                    
                    // Cascade delete for replies
                    var replies = await _firestoreDb.Collection("Comments").WhereEqualTo("ParentId", commentId).GetSnapshotAsync();
                    foreach (var reply in replies.Documents)
                    {
                        await reply.Reference.DeleteAsync();
                    }
                    return Ok();
                }
                
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }
    }

    public class AddCommentRequest
    {
        public string LessonId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}
