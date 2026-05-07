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
                DocumentSnapshot snapshot = await Db.Collection("Users").Document(uid).GetSnapshotAsync();
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

                DocumentReference docRef = Db.Collection("Users").Document(uid);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                string role = email == "buitrantrongnguyen@gmail.com" ? "Teacher" : "Student";

                // Kiểm tra xem user đã tồn tại chưa để tránh ghi đè
                if (!snapshot.Exists)
                {
                    Dictionary<string, object> user = new Dictionary<string, object>
                    {
                        { "Uid", uid },
                        { "Email", email },
                        { "FullName", string.IsNullOrEmpty(displayName) ? "New User" : displayName },
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
    }
}