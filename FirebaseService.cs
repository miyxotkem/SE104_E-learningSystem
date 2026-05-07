using e_learning_app.Class;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FirebaseAdmin;
using Google.Cloud.Firestore;
using FirebaseAdmin.Auth;

namespace e_learning_app
{
    public static class FirebaseService
    {
        private const string ApiKey = "AIzaSyDU5RuicqibEcEx5dmagllQ14WOJ467yic";
        private const string ProjectId = "e-learning-cd1b3";
        private static FirebaseAuthClient ?_authClient;
        public static FirestoreDb ?Db { get; private set; }

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

                // 2. Khởi tạo Firestore 
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase", "firebase_json.json");
                if (File.Exists(path))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
                    Db = FirestoreDb.Create(ProjectId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi Init: " + ex.Message);
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
        public static async Task<string> LoginWithGoogleAsync()
        {
            string credPath = "gg.auth.api";
            var dataStore = new FileDataStore(credPath, true);

            try
            {
                // Xóa cache cũ để có thể chọn tài khoản Google khác
                await dataStore.ClearAsync();

                string[] scopes = { "openid", "email", "profile" };
                var secrets = new ClientSecrets
                {
                    ClientId = GoogleClientId,
                    ClientSecret = GoogleClientSecret
                };
                var googleCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    dataStore);

                string idToken = googleCredential.Token.IdToken;
                string accessToken = googleCredential.Token.AccessToken;

                if (_authClient == null) return null;

                var credential = GoogleProvider.GetCredential(idToken, OAuthCredentialTokenType.IdToken);
                var authResult = await _authClient.SignInWithCredentialAsync(credential);

                return authResult.User.Uid;
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
                    MessageBox.Show("Lỗi: " + ex.Message);
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
        public static async Task<bool> CreateUserInFirestore(string uid, string email = "", string displayName = "")
        {
            try
            {
                if (Db == null)
                {
                    MessageBox.Show("Chưa kết nối được Firestore!");
                    return false;
                }

                DocumentReference docRef = Db.Collection("userss").Document(uid);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                // Kiểm tra xem user đã tồn tại chưa để tránh ghi đè
                if (!snapshot.Exists)
                {
                    Dictionary<string, object> user = new Dictionary<string, object>
                    {
                        { "Uid", uid },
                        { "Email", email },
                        { "DisplayName", string.IsNullOrEmpty(displayName) ? "New User" : displayName },
                        { "CreatedAt", FieldValue.ServerTimestamp },
                        { "Role", "Student" }
                    };

                    await docRef.SetAsync(user);
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