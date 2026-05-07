using Google.Cloud.Firestore;
using System;

namespace e_learning_app
{
    [FirestoreData]
    public class User
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        // Convenience alias used in Admin views
        public string Uid => Id;

        [FirestoreProperty]
        public string Username { get; set; }

        [FirestoreProperty]
        public string Password { get; set; }

        [FirestoreProperty]
        public string Email { get; set; }

        [FirestoreProperty]
        public string FullName { get; set; }

        [FirestoreProperty]
        public string PhoneNumber { get; set; }

        [FirestoreProperty]
        public string Role { get; set; }

        [FirestoreProperty]
        public System.DateTime? CreatedAt { get; set; }
    }
}