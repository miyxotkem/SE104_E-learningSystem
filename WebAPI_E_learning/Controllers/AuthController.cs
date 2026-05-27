using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Firestore;
using WebAPI_E_learning.Models;
using System.Net.Http;
using System.Text.Json;

namespace WebAPI_E_learning.Controllers

{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly FirestoreDb _firestoreDb;

        public AuthController(IConfiguration config, FirestoreDb firestoreDb)
        {
            _config = config;
            _firestoreDb = firestoreDb;
        }

        public class LoginRequest
        {
            public string FirebaseToken { get; set; }
        }

        public class LoginEmailRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class GoogleAuthResult
        {
            public string LocalId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string IdToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public string ExpiresIn { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginEmailRequest request)
        {
            try
            {
                // -- Admin đặc biệt --
                if (request.Email == "admin" && request.Password == "admin")
                {
                    string uid = "admin_super_id";
                    string role = "Admin";
                    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
                    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, uid),
                        new Claim(ClaimTypes.Email, "admin@system.com"),
                        new Claim(ClaimTypes.Role, role)
                    };
                    var token = new JwtSecurityToken(_config["Jwt:Issuer"], _config["Jwt:Audience"], claims, expires: DateTime.Now.AddHours(24), signingCredentials: credentials);
                    var jwtToReturn = new JwtSecurityTokenHandler().WriteToken(token);

                    return Ok(new
                    {
                        Token = jwtToReturn,
                        Uid = uid,
                        Email = "admin@system.com",
                        DisplayName = "Administrator",
                        FirebaseToken = "fake-token",
                        ExpiresIn = 86400
                    });
                }

                using (var httpClient = new HttpClient())
                {
                    var apiKey = _config["Firebase:ApiKey"];
                    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";
                    var payload = new
                    {
                        email = request.Email,
                        password = request.Password,
                        returnSecureToken = true
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        string errorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                        try
                        {
                            var errDoc = JsonDocument.Parse(errorResponse).RootElement;
                            if (errDoc.TryGetProperty("error", out var errProp) && errProp.TryGetProperty("message", out var msgProp))
                            {
                                string firebaseError = msgProp.GetString();
                                if (firebaseError == "EMAIL_NOT_FOUND" || firebaseError == "INVALID_PASSWORD" || firebaseError == "INVALID_LOGIN_CREDENTIALS")
                                {
                                    errorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                                }
                                else if (firebaseError == "USER_DISABLED")
                                {
                                    errorMessage = "Tài khoản này đã bị vô hiệu hóa.";
                                }
                                else
                                {
                                    errorMessage = firebaseError;
                                }
                            }
                        }
                        catch {}
                        return Unauthorized(new { Message = errorMessage });
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    var googleAuthResult = JsonSerializer.Deserialize<GoogleAuthResult>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (googleAuthResult == null || string.IsNullOrEmpty(googleAuthResult.LocalId))
                    {
                        return Unauthorized(new { Message = "Không thể xác thực thông tin đăng nhập." });
                    }

                    var uid = googleAuthResult.LocalId;
                    var userDoc = await _firestoreDb.Collection("Users").Document(uid).GetSnapshotAsync();
                    string role = "Student";
                    bool isBlocked = false;
                    if (userDoc.Exists)
                    {
                        if (userDoc.TryGetValue("Role", out string dbRole))
                        {
                            role = dbRole;
                        }
                        if (userDoc.TryGetValue("IsBlocked", out bool dbBlocked))
                        {
                            isBlocked = dbBlocked;
                        }
                    }

                    if (isBlocked)
                    {
                        return BadRequest(new { Message = "Tài khoản của bạn đã bị khóa bởi Admin!" });
                    }

                    // Tạo JWT nội bộ của hệ thống
                    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
                    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, uid),
                        new Claim(ClaimTypes.Email, googleAuthResult.Email),
                        new Claim(ClaimTypes.Role, role)
                    };
                    var token = new JwtSecurityToken(
                        _config["Jwt:Issuer"],
                        _config["Jwt:Audience"],
                        claims,
                        expires: DateTime.Now.AddHours(24), // Token có hạn 24 tiếng
                        signingCredentials: credentials);

                    var jwtToReturn = new JwtSecurityTokenHandler().WriteToken(token);

                    return Ok(new
                    {
                        Token = jwtToReturn,
                        Uid = uid,
                        Email = googleAuthResult.Email,
                        DisplayName = googleAuthResult.DisplayName,
                        FirebaseToken = googleAuthResult.IdToken,
                        ExpiresIn = int.TryParse(googleAuthResult.ExpiresIn, out int exp) ? exp : 3600
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Lỗi xử lý đăng nhập hệ thống", Error = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var userArgs = new UserRecordArgs()
                {
                    Email = request.Email,
                    Password = request.Password,
                    DisplayName = request.FullName,
                };
                UserRecord userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);

                var userDoc = _firestoreDb.Collection("Users").Document(userRecord.Uid);
                var userData = new Dictionary<string, object>
                {
                    { "Email", request.Email },
                    { "FullName", request.FullName },
                    { "Role", "Student" },
                    { "PhoneNumber", "" },
                    { "CreatedAt", DateTime.UtcNow },
                    { "IsBlocked", false },
                    { "ProfileImageUrl", "" }
                };
                await userDoc.SetAsync(userData);

                return Ok(new { Message = "Đăng ký thành công", Uid = userRecord.Uid });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Lỗi đăng ký", Error = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var apiKey = _config["Firebase:ApiKey"];
                    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={apiKey}";
                    var payload = new
                    {
                        requestType = "PASSWORD_RESET",
                        email = request.Email
                    };

                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorResponse = await response.Content.ReadAsStringAsync();
                        return BadRequest(new { Message = "Lỗi gửi yêu cầu khôi phục mật khẩu", Error = errorResponse });
                    }

                    return Ok(new { Message = "Đã gửi email khôi phục mật khẩu!" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Lỗi yêu cầu reset password", Error = ex.Message });
            }
        }

        [HttpPost("Login-google")]
        public async Task<IActionResult> LoginGoogle([FromBody] LoginRequest request)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var apiKey = _config["Firebase:ApiKey"];
                    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={apiKey}";
                    var payload = new
                    {
                        postBody = $"id_token={request.FirebaseToken}&providerId=google.com",
                        requestUri = "http://localhost",
                        returnIdToken = true,
                        returnSecureToken = true
                    };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errStr = await response.Content.ReadAsStringAsync();
                        return Unauthorized(new { Message = "Xác thực với Google Identity Toolkit thất bại.", Error = errStr });
                    }

                    var resStr = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(resStr);
                    var root = doc.RootElement;

                    string uid = root.GetProperty("localId").GetString();
                    string email = root.GetProperty("email").GetString();
                    string displayName = root.TryGetProperty("displayName", out var dp) ? dp.GetString() : email;
                    string photoUrl = root.TryGetProperty("photoUrl", out var pu) ? pu.GetString() : "";

                    var userDocRef = _firestoreDb.Collection("Users").Document(uid);
                    var userDoc = await userDocRef.GetSnapshotAsync();
                    string role = "Student";
                    bool isBlocked = false;

                    if (!userDoc.Exists)
                    {
                        var userData = new Dictionary<string, object>
                        {
                            { "Email", email },
                            { "FullName", displayName },
                            { "Role", "Student" },
                            { "PhoneNumber", "" },
                            { "CreatedAt", DateTime.UtcNow },
                            { "IsBlocked", false },
                            { "ProfileImageUrl", photoUrl }
                        };
                        await userDocRef.SetAsync(userData);
                    }
                    else
                    {
                        if (userDoc.TryGetValue("Role", out string dbRole))
                        {
                            role = dbRole;
                        }
                        if (userDoc.TryGetValue("IsBlocked", out bool dbBlocked))
                        {
                            isBlocked = dbBlocked;
                        }
                    }

                    if (isBlocked)
                    {
                        return BadRequest(new { Message = "Tài khoản của bạn đã bị khóa bởi Admin!" });
                    }

                    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
                    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, uid),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, role)
                    };
                    var token = new JwtSecurityToken(
                        _config["Jwt:Issuer"],
                        _config["Jwt:Audience"],
                        claims,
                        expires: DateTime.Now.AddHours(24),
                        signingCredentials: credentials);

                    var jwtToReturn = new JwtSecurityTokenHandler().WriteToken(token);

                    return Ok(new
                    {
                        Token = jwtToReturn,
                        Uid = uid,
                        Email = email,
                        DisplayName = displayName,
                        FirebaseToken = root.GetProperty("idToken").GetString(),
                        ExpiresIn = int.TryParse(root.GetProperty("expiresIn").GetString(), out int exp) ? exp : 3600
                    });
                }
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Message = "Token không hợp lệ", Error = ex.Message });
            }
        }
    }
}

