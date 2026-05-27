namespace WebAPI_E_learning.Models
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
    }

    public class UpdateAvatarRequest
    {
        public string ProfileImageUrl { get; set; } = string.Empty;
    }

    public class ChangeRoleRequest
    {
        public string Role { get; set; } = "Student";
    }

    public class BlockUserRequest
    {
        public bool IsBlocked { get; set; }
    }
}
