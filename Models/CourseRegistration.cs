using Google.Cloud.Firestore;
using System;

namespace e_learning_app.Class
{
    [FirestoreData]
    public class CourseRegistration
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty]
        public string userId { get; set; }

        [FirestoreProperty]
        public string courseId { get; set; }

        [FirestoreProperty]
        public string status { get; set; }

        [FirestoreProperty]
        public DateTime? requestDate { get; set; }

        [FirestoreProperty]
        public DateTime? approvedDate { get; set; }

        [FirestoreProperty]
        public string fullName { get; set; }

        [FirestoreProperty]
        public string email { get; set; }
    }
}