using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class Comment : System.ComponentModel.INotifyPropertyChanged
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = null!;

        [FirestoreProperty]
        public string LessonId { get; set; } = null!;

        [FirestoreProperty]
        public string ParentId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string UserId { get; set; } = null!;

        [FirestoreProperty]
        public string UserName { get; set; } = null!;

        [FirestoreProperty]
        public string UserRole { get; set; } = null!; // Student, Teacher

        private string _profileImageUrl = string.Empty;

        [FirestoreProperty]
        public string ProfileImageUrl 
        { 
            get => string.IsNullOrEmpty(_profileImageUrl) ? null : _profileImageUrl;
            set => _profileImageUrl = value; 
        }

        [FirestoreProperty]
        public string Content { get; set; } = null!;

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // UI Properties (Not in Firestore)
        private bool _isReplying;
        public bool IsReplying 
        { 
            get => _isReplying; 
            set { _isReplying = value; OnPropertyChanged(nameof(IsReplying)); OnPropertyChanged(nameof(ReplyInputVisibility)); } 
        }

        public System.Windows.Visibility ReplyInputVisibility => IsReplying ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private string _replyText = string.Empty;
        public string ReplyText
        {
            get => _replyText;
            set { _replyText = value; OnPropertyChanged(nameof(ReplyText)); }
        }

        public System.Collections.ObjectModel.ObservableCollection<Comment> Replies { get; set; } = new System.Collections.ObjectModel.ObservableCollection<Comment>();

        public bool CanDelete { get; set; }
        public System.Windows.Visibility DeleteButtonVisibility => CanDelete ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private static readonly string[] AvatarColors = new string[]
        {
            "#3B82F6", // Blue
            "#10B981", // Emerald
            "#8B5CF6", // Violet
            "#F59E0B", // Amber
            "#EF4444", // Red
            "#EC4899", // Pink
            "#06B6D4", // Cyan
            "#F97316", // Orange
            "#14B8A6", // Teal
            "#6366F1"  // Indigo
        };

        public string AvatarColor
        {
            get
            {
                if (string.IsNullOrEmpty(UserName)) return "#64748B"; // Slate default
                int hash = 0;
                foreach (char c in UserName)
                {
                    hash += c;
                }
                int index = Math.Abs(hash) % AvatarColors.Length;
                return AvatarColors[index];
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
