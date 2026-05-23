using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace e_learning_app.Class
{
    public static class SecureTokenManager
    {
        private static readonly string _filePath;

        static SecureTokenManager()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EduSmart");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "secure_token.dat");
        }

        public static void SaveToken(string jwt)
        {
            // Mã hóa chuỗi JWT
            byte[] rawData = Encoding.UTF8.GetBytes(jwt);
            byte[] encryptedData = ProtectedData.Protect(rawData, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_filePath, encryptedData);
        }

        public static string GetToken()
        {
            if (!File.Exists(_filePath)) return null;

            try
            {
                byte[] encryptedData = File.ReadAllBytes(_filePath);
                // Giải mã
                byte[] rawData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(rawData);
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"[SecureToken] Giải mã thất bại: {ex.Message}");
                DeleteToken();
                return null;
            }
        }

        public static void DeleteToken()
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
    }
}