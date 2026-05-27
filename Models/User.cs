using Google.Cloud.Firestore;
using System;

using System.ComponentModel;

namespace e_learning_app
{
    [FirestoreData]
    public class User : INotifyPropertyChanged
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        // Convenience alias used in Admin views
        public string Uid => Id;

        [FirestoreProperty]
        public string Email { get; set; }

        private string _fullName;
        [FirestoreProperty]
        public string FullName 
        { 
            get => _fullName; 
            set { _fullName = value; OnPropertyChanged(nameof(FullName)); } 
        }

        [FirestoreProperty]
        public string PhoneNumber { get; set; }

        [FirestoreProperty]
        public string Role { get; set; }

        [FirestoreProperty]
        public bool IsBlocked { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        private string _profileImageUrl;
        [FirestoreProperty]
        public string ProfileImageUrl 
        { 
            get => _profileImageUrl; 
            set { _profileImageUrl = value; OnPropertyChanged(nameof(ProfileImageUrl)); } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}