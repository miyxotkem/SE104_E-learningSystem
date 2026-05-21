using System;

namespace e_learning_app.Class
{
    public class AssignmentResponse
    {
        public string Id { get; set; } = string.Empty;
        public Assignment Data { get; set; }

        // Shortcut properties for easier access in views
        public string Title => Data?.Title ?? string.Empty;
        public string Description => Data?.Description ?? string.Empty;
        public DateTime Deadline => Data?.Deadline ?? default;
        public string AttachedFileUrl => Data?.AttachedFileUrl ?? string.Empty;
        public bool IsGradesPublished => Data?.IsGradesPublished ?? false;
    }
}
