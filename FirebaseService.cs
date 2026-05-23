using e_learning_app.Class;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace e_learning_app
{
    public class UserSession
    {
        public string Uid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty; // System JWT
        public string FirebaseToken { get; set; } = string.Empty; // Firebase ID Token (for direct Firestore queries for now)
        public int ExpiresIn { get; set; }
        public DateTime SavedTime { get; set; }
    }

    public static class SessionManager
    {
        private static readonly string _filePath;

        static SessionManager()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EduSmart");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "auth.json");
        }

        public static bool SessionExists() => File.Exists(_filePath);

        public static UserSession ReadSession()
        {
            if (!File.Exists(_filePath)) return null;
            try
            {
                var json = File.ReadAllText(_filePath);
                var session = JsonSerializer.Deserialize<UserSession>(json);
                if (session == null || string.IsNullOrEmpty(session.Token)) return null;
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Session Read Error: " + ex.Message);
                return null;
            }
        }

        public static void SaveSession(UserSession session)
        {
            try
            {
                session.SavedTime = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(session);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Save Session Error: " + ex.Message);
            }
        }

        public static void DeleteSession()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    File.Delete(_filePath);
                }
                catch {}
            }
        }
    }

    // Mock classes to maintain 100% compatibility with other parts of the codebase
    public class MockUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class MockCredential
    {
        public string IdToken { get; set; } = string.Empty;
    }

    public class MockUser
    {
        public string Uid { get; set; } = string.Empty;
        public MockUserInfo Info { get; set; } = new MockUserInfo();
        public MockCredential Credential { get; set; } = new MockCredential();
    }

    public class MockFirebaseAuthClient
    {
        public MockUser User { get; set; }
        public void SignOut()
        {
            FirebaseService.SignOut();
        }
    }

    public class SimpleUserRepository
    {
        public bool UserExists() => SessionManager.SessionExists();
        public void DeleteUser() => SessionManager.DeleteSession();
    }

    public static class FirebaseService
    {
        private const string ProjectId = "e-learning-cd1b3";
        private static readonly MockFirebaseAuthClient _mockAuth = new MockFirebaseAuthClient();
        public static MockFirebaseAuthClient Auth => _mockAuth;
        // Db is always null — DatabaseManager no longer uses Firestore directly
        public static object Db => null;
        private static UserSession _currentSession;
        public static UserSession CurrentSession => _currentSession;

        public static void Initialize()
        {
            try
            {
                // Firestore SDK đã bị loại bỏ. Tất cả dữ liệu truy cập qua REST API.
                // Phục hồi session cũ nếu có
                if (SessionManager.SessionExists())
                {
                    var session = SessionManager.ReadSession();
                    if (session != null)
                    {
                        _currentSession = session;
                        _mockAuth.User = new MockUser
                        {
                            Uid = session.Uid,
                            Info = new MockUserInfo { Email = session.Email, DisplayName = session.DisplayName },
                            Credential = new MockCredential { IdToken = session.FirebaseToken }
                        };

                        // Khôi phục Token lên ApiService
                        SecureTokenManager.SaveToken(session.Token);
                        ApiService.SetJwtToken(session.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi khởi tạo ứng dụng: " + ex.Message, "Lỗi", DialogType.Error);
            }
        }

        public static void SignOut()
        {
            try
            {
                SessionManager.DeleteSession();
                _currentSession = null;
                _mockAuth.User = null;
                SecureTokenManager.SaveToken("");
                ApiService.SetJwtToken("");
            }
            catch { }
        }

        public static async Task<string> LoginAsync(string email, string password)
        {
            var result = await LoginWithTokenAsync(email, password);
            return result.Uid;
        }

        public class BackendLoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class BackendLoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public string Uid { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string FirebaseToken { get; set; } = string.Empty;
            public int ExpiresIn { get; set; }
        }

        public static async Task<(string Uid, string Token)> LoginWithTokenAsync(string email, string password)
        {
            try
            {
                var req = new BackendLoginRequest { Email = email, Password = password };
                var response = await ApiService.PostAsync<BackendLoginRequest, BackendLoginResponse>("auth/login", req);

                if (response == null || string.IsNullOrEmpty(response.Token))
                {
                    return (null, null);
                }

                // Lưu session
                var session = new UserSession
                {
                    Uid = response.Uid,
                    Email = response.Email,
                    DisplayName = response.DisplayName,
                    Token = response.Token,
                    FirebaseToken = response.FirebaseToken,
                    ExpiresIn = response.ExpiresIn
                };
                SessionManager.SaveSession(session);

                _currentSession = session;
                _mockAuth.User = new MockUser
                {
                    Uid = response.Uid,
                    Info = new MockUserInfo { Email = response.Email, DisplayName = response.DisplayName },
                    Credential = new MockCredential { IdToken = response.FirebaseToken }
                };

                SecureTokenManager.SaveToken(response.Token);
                ApiService.SetJwtToken(response.Token);

                return (response.Uid, response.FirebaseToken);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        public class BackendRegisterRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
        }

        public class BackendRegisterResponse
        {
            public string Message { get; set; } = string.Empty;
            public string Uid { get; set; } = string.Empty;
        }

        public static async Task<string> RegisterAsync(string email, string password, string fullName)
        {
            try
            {
                var req = new BackendRegisterRequest 
                { 
                    Email = email, 
                    Password = password,
                    FullName = fullName
                };
                var response = await ApiService.PostAsync<BackendRegisterRequest, BackendRegisterResponse>("auth/register", req);

                if (response == null || string.IsNullOrEmpty(response.Uid))
                {
                    return null;
                }

                return response.Uid;
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi đăng ký: " + ex.Message, "Lỗi", DialogType.Error);
                return null;
            }
        }

        //GOOGLEEEEEEEEEEEEE
        private const string GoogleClientId = "105514257729-ienl99san19bis48vav5lppchd7fuf1j.apps.googleusercontent.com";
        private const string GoogleClientSecret = "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";

        public static async Task<MockUser> LoginWithGoogleAsync()
        {
            string credPath = "gg.auth.api";
            var dataStore = new FileDataStore(credPath, true);

            try
            {
                var secrets = new ClientSecrets
                {
                    ClientId = GoogleClientId,
                    ClientSecret = GoogleClientSecret
                };

                string[] scopes = { "openid", "email", "profile" };
                var receiver = new EduSmartCodeReceiver();

                var googleCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    dataStore,
                    receiver);

                string idToken = googleCredential.Token.IdToken;

                var authResponse = await AuthService.AuthenticateWithBackendAsync(idToken);
                if (authResponse == null)
                {
                    await dataStore.ClearAsync();
                    CustomDialog.Show("Không thể xác thực thông tin đăng nhập Google với Backend. Vui lòng kiểm tra lại kết nối mạng hoặc liên hệ Admin.", "Lỗi xác thực", DialogType.Error);
                    return null;
                }

                string email = authResponse.Email;
                string displayName = authResponse.DisplayName;
                string uid = authResponse.Uid;
                string realFirebaseToken = authResponse.FirebaseToken;

                var session = new UserSession
                {
                    Uid = uid,
                    Email = email,
                    DisplayName = displayName,
                    Token = authResponse.Token,
                    FirebaseToken = realFirebaseToken
                };
                SessionManager.SaveSession(session);

                _currentSession = session;
                _mockAuth.User = new MockUser
                {
                    Uid = uid,
                    Info = new MockUserInfo { Email = email, DisplayName = displayName },
                    Credential = new MockCredential { IdToken = realFirebaseToken }
                };

                return _mockAuth.User;
            }
            catch (Exception ex)
            {
                await dataStore.ClearAsync();
                if (ex.Message.Contains("stale") || ex.Message.Contains("INVALID_ID_RESPONSE"))
                {
                    CustomDialog.Show("Phiên đăng nhập cũ bị lỗi. Hệ thống đã tự động làm mới, vui lòng nhấn Đăng nhập lại một lần nữa nhé!", "Thông báo", DialogType.Info);
                }
                else
                {
                    CustomDialog.Show("Lỗi đăng nhập Google: " + ex.Message, "Lỗi", DialogType.Error);
                }
                return null;
            }
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        public static async Task<bool> SendPasswordResetAsync(string email)
        {
            try
            {
                var req = new ResetPasswordRequest { Email = email };
                bool success = await ApiService.PostAsync("auth/forgot-password", req);
                return success;
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
                var resp = await ApiService.GetAsync<e_learning_app.Class.UserResponse>($"users/{uid}");
                return resp?.Data?.Role ?? "Student";
            }
            catch
            {
                return "Student";
            }
        }

        public static async Task<bool> CreateUserInFirestore(string uid, string email = "", string fullName = "")
        {
            try
            {
                var payload = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Uid", uid },
                    { "Email", email },
                    { "FullName", string.IsNullOrEmpty(fullName) ? "New User" : fullName }
                };
                var response = await ApiService.PostAsync<dynamic>("users/sync-user", payload);
                return response != null;
            }
            catch (Exception ex)
            {
                CustomDialog.Show("Lỗi tạo user trong Firestore: " + ex.Message, "Lỗi", DialogType.Error);
                return false;
            }
        }        
    }
}
