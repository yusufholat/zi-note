using Google.Cloud.Firestore;
using Zinote.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zinote.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _db;
        private const string CollectionName = "dictionary_items";

        public FirestoreService()
        {
            // Set environment variable for credentials (we'll use application default)
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "");
            
            // Initialize Firestore with project ID
            _db = FirestoreDb.Create("zinote-83c37");
        }

        public async Task<List<DictionaryItem>> GetAllAsync()
        {
            var snapshot = await _db.Collection(CollectionName).GetSnapshotAsync();
            var items = new List<DictionaryItem>();

            foreach (var document in snapshot.Documents)
            {
                if (document.Exists)
                {
                    var item = document.ConvertTo<DictionaryItem>();
                    item.Id = document.Id;
                    items.Add(item);
                }
            }

            return items;
        }

        public async Task AddAsync(DictionaryItem item)
        {
            var docRef = _db.Collection(CollectionName).Document(item.Id);
            await docRef.SetAsync(item);
        }

        public async Task UpdateAsync(DictionaryItem item)
        {
            var docRef = _db.Collection(CollectionName).Document(item.Id);
            await docRef.SetAsync(item, SetOptions.MergeAll);
        }

        public async Task DeleteAsync(string id)
        {
            var docRef = _db.Collection(CollectionName).Document(id);
            await docRef.DeleteAsync();
        }

        public List<DictionaryItem> Search(List<DictionaryItem> items, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return items;

            return items.FindAll(x =>
                (x.SourceTerm?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.TargetTerm?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Definition?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }
}
