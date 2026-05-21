using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using e_learning_app.Class;

namespace e_learning_app
{
    public class GoogleAuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FirebaseToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    public static class AuthService
    {
        private const string ApiBaseUrl = "https://api-e-learning.thankfulflower-208a0ec8.eastasia.azurecontainerapps.io/api";
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<GoogleAuthResponse> AuthenticateWithBackendAsync(string firebaseIdToken)
        {
            try
            {
                var payload = new { FirebaseToken = firebaseIdToken };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/auth/login-google", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var googleAuthResponse = JsonSerializer.Deserialize<GoogleAuthResponse>(jsonResponse, options);

                    if (googleAuthResponse != null && !string.IsNullOrEmpty(googleAuthResponse.Token))
                    {
                        SecureTokenManager.SaveToken(googleAuthResponse.Token);
                        ApiService.SetJwtToken(googleAuthResponse.Token);
                        return googleAuthResponse;
                    }
                }
                else
                {
                    var errContent = await response.Content.ReadAsStringAsync();
                    string errMsg = "Xác thực với Backend thất bại";
                    try
                    {
                        using var doc = JsonDocument.Parse(errContent);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("message", out var mProp)) errMsg = mProp.GetString();
                        else if (root.TryGetProperty("Message", out var mProp2)) errMsg = mProp2.GetString();
                    }
                    catch {}
                    throw new Exception($"{errMsg} (HTTP {(int)response.StatusCode})");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error authenticating with backend: " + ex.Message);
                throw;
            }
        }
    }
}
