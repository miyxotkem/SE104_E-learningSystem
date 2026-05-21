using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace e_learning_app.Class
{
    public class FirestoreDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (DateTime.TryParse(reader.GetString(), out DateTime dt))
                    return dt;
                return DateTime.MinValue;
            }
            
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                long seconds = 0;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string prop = reader.GetString();
                        reader.Read();
                        if (prop == "seconds" || prop == "_seconds")
                        {
                            seconds = reader.GetInt64();
                        }
                    }
                }
                return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }

            return DateTime.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }

    public static class ApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
#if DEBUG
        private const string BaseUrl = "http://localhost:5105/api";
#else
        private const string BaseUrl = "https://api-e-learning.thankfulflower-208a0ec8.eastasia.azurecontainerapps.io/api";
#endif  

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new FirestoreDateTimeConverter() }
        };

        public static void SetJwtToken(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        public static async Task<T> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/{endpoint}");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }

        public static async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/{endpoint}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    e_learning_app.CustomDialog.Show($"Server Azure đã báo lỗi (HTTP {(int)response.StatusCode}):\n\n{error}", "Lỗi từ Backend", e_learning_app.DialogType.Error);
                });
                return default;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(json, _jsonOptions);
        }

        public static async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data)
        {
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/{endpoint}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    e_learning_app.CustomDialog.Show($"Server Azure đã báo lỗi (HTTP {(int)response.StatusCode}):\n\n{error}", "Lỗi từ Backend", e_learning_app.DialogType.Error);
                });
                return false;
            }

            return response.IsSuccessStatusCode;
        }
        
        public static async Task<bool> PutAsync<TRequest>(string endpoint, TRequest data)
        {
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{BaseUrl}/{endpoint}", content);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> DeleteAsync(string endpoint)
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}/{endpoint}");
            return response.IsSuccessStatusCode;
        }
    }
}
