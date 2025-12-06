using System;
using Google.Cloud.Firestore;

namespace Zinote.Models
{
    [FirestoreData]
    public class DictionaryItem
    {
        [FirestoreProperty]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [FirestoreProperty]
        public string SourceTerm { get; set; }

        [FirestoreProperty]
        public string TargetTerm { get; set; }

        [FirestoreProperty]
        public string Definition { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string CreatedBy { get; set; }

        [FirestoreProperty]
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        public string ModifiedBy { get; set; }

        [FirestoreProperty]
        public bool IsDeleted { get; set; } = false;

        [FirestoreProperty]
        public DateTime? DeletedAt { get; set; }

        [FirestoreProperty]
        public string DeletedBy { get; set; }

        [FirestoreProperty]
        public string Domain { get; set; }

        [FirestoreProperty]
        public string SubDomain { get; set; }

        [FirestoreProperty]
        public string Notes { get; set; }

        [FirestoreProperty]
        public string ExampleOfUse { get; set; }

        [FirestoreProperty]
        public bool Forbidden { get; set; } = false;
    }
}
