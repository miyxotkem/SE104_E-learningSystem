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
    public class UsersController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public UsersController(FirestoreDb firestoreDb)
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

        [HttpPost("sync-user")]
        public async Task<IActionResult> SyncUser([FromBody] Dictionary<string, string> request)
        {
            string uid = GetCurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            var docRef = _firestoreDb.Collection("Users").Document(uid);
            var docSnap = await docRef.GetSnapshotAsync();

            if (!docSnap.Exists)
            {
                var newUser = new Dictionary<string, object>
                {
                    { "Uid", uid },
                    { "FullName", request.ContainsKey("FullName") ? request["FullName"] : "User" },
                    { "Email", request.ContainsKey("Email") ? request["Email"] : "" },
                    { "Role", "Student" },
                    { "CreatedAt", DateTime.UtcNow },
                    { "IsBlocked", false },
                    { "Provider", request.ContainsKey("Provider") ? request["Provider"] : "email" },
                    { "ProfileImageUrl", request.ContainsKey("PhotoUrl") ? request["PhotoUrl"] : "" }
                };
                await docRef.SetAsync(newUser);
                return Ok(new { Message = "User synchronized.", User = newUser });
            }

            return Ok(new { Message = "User already exists.", User = ConvertFirestoreTypes(docSnap.ToDictionary()) });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            string uid = GetCurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            var doc = await _firestoreDb.Collection("Users").Document(uid).GetSnapshotAsync();
            if (!doc.Exists) return NotFound(new { Message = "User profile not found." });

            return Ok(ConvertFirestoreTypes(doc.ToDictionary()));
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            string uid = GetCurrentUserId();
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            var docRef = _firestoreDb.Collection("Users").Document(uid);
            await docRef.UpdateAsync("FullName", request.FullName);

            return Ok(new { Message = "Profile updated successfully." });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var snapshot = await _firestoreDb.Collection("Users").GetSnapshotAsync();
                var users = snapshot.Documents.Select(d => new
                {
                    Id = d.Id,
                    Data = ConvertFirestoreTypes(d.ToDictionary())
                });

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi khi lấy danh sách user từ Backend", Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserById(string id)
        {
            var doc = await _firestoreDb.Collection("Users").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound(new { Message = "User not found." });
            return Ok(new { Id = doc.Id, Data = ConvertFirestoreTypes(doc.ToDictionary()) });
        }

        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleRequest request)
        {
            var docRef = _firestoreDb.Collection("Users").Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists) return NotFound(new { Message = "User not found." });

            await docRef.UpdateAsync("Role", request.Role);
            return Ok(new { Message = "Role updated successfully." });
        }

        [HttpPut("{id}/block")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BlockUser(string id, [FromBody] BlockUserRequest request)
        {
            var docRef = _firestoreDb.Collection("Users").Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists) return NotFound(new { Message = "User not found." });

            await docRef.UpdateAsync("IsBlocked", request.IsBlocked);
            return Ok(new { Message = $"User {(request.IsBlocked ? "blocked" : "unblocked")} successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var docRef = _firestoreDb.Collection("Users").Document(id);
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists) return NotFound("User not found");

            await docRef.DeleteAsync();
            return Ok(new { Message = "User deleted successfully" });
        }
    }
}
