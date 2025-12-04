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
    }
}
