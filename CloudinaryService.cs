using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace e_learning_app
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        // BẠN CẦN THAY THẾ 3 CHUỖI NÀY BẰNG THÔNG TIN TỪ DASHBOARD CLOUDINARY CỦA BẠN
        private const string CLOUD_NAME = "drg8swbxp";
        private const string API_KEY = "642961242338667";
        private const string API_SECRET = "4MT0IhuHZgw-mDVghDdGg2Ufakk";

        public CloudinaryService()
        {
            Account account = new Account(CLOUD_NAME, API_KEY, API_SECRET);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        /// <summary>
        /// Upload một file hình ảnh lên Cloudinary
        /// </summary>
        /// <param name="filePath">Đường dẫn tới file ảnh trên máy tính (ví dụ: C:\Images\avatar.jpg)</param>
        /// <param name="folderName">Tên thu mục trên Cloudinary (nếu muốn gom nhóm ảnh)</param>
        /// <returns>URL an toàn (https) của ảnh sau khi upload thành công</returns>
        public async Task<string?> UploadImageAsync(string filePath, string folderName = "e_learning_images")
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Không tìm thấy file để upload", filePath);

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(filePath),
                Folder = folderName,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Lỗi khi upload ảnh lên Cloudinary: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl?.ToString();
        }

        /// <summary>
        /// Upload một file video lên Cloudinary
        /// </summary>
        /// <param name="filePath">Đường dẫn tới file video trên máy tính</param>
        /// <param name="folderName">Tên thu mục trên Cloudinary</param>
        /// <returns>URL an toàn (https) của video sau khi upload thành công</returns>
        public async Task<string?> UploadVideoAsync(string filePath, string folderName = "e_learning_videos")
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Không tìm thấy file để upload", filePath);

            var uploadParams = new VideoUploadParams()
            {
                File = new FileDescription(filePath),
                Folder = folderName,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Lỗi khi upload video lên Cloudinary: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl?.ToString();
        }

        /// <summary>
        /// Xóa một file (ảnh hoặc video) trên Cloudinary bằng PublicId
        /// </summary>
        public async Task<bool> DeleteFileAsync(string publicId, ResourceType resourceType = ResourceType.Image)
        {
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType
            };

            var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

            return deletionResult.Result == "ok";
        }
    }
}
