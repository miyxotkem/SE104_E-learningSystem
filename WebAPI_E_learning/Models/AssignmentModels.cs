namespace WebAPI_E_learning.Models
{
    public class CreateAssignmentRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public string AttachedFileUrl { get; set; } = string.Empty;
    }

    public class SubmitAssignmentRequest
    {
        public string FileUrl { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
    public class UpdateAssignmentRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime Deadline { get; set; }
        public string? AttachedFileUrl { get; set; }
    }

    public class GradeSubmissionRequest
    {
        public double? Score { get; set; }
        public string? Comment { get; set; }
    }
}
