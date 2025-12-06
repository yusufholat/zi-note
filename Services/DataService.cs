using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zinote.Models;
using Google.Cloud.Firestore;
using ClosedXML.Excel;
using Zinote.Helpers;
using System.Text.Json;

namespace Zinote.Services
{
    public class DataService
    {
        private FirestoreDb _firestoreDb;
        private readonly AuthService _authService;

        public DataService(AuthService authService)
        {
            _authService = authService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Path to the credentials file
                string credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.CredentialsFileName);
                
                if (!File.Exists(credentialsPath))
                {
                    var current = AppDomain.CurrentDomain.BaseDirectory;
                    for (int i = 0; i < 5; i++)
                    {

                        var tryPath = Path.Combine(current, Constants.CredentialsFileName);
                        if (File.Exists(tryPath))
                        {
                            credentialsPath = tryPath;
                            break;
                        }
                        var parent = Directory.GetParent(current);
                        if (parent == null) break;
                        current = parent.FullName;
                    }
                }

                if (File.Exists(credentialsPath))
                {
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
                    
                    // Read project_id from json
                    string jsonContent = await File.ReadAllTextAsync(credentialsPath);
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        if (doc.RootElement.TryGetProperty("project_id", out JsonElement projectIdElement))
                        {
                            string projectId = projectIdElement.GetString();
                            _firestoreDb = await FirestoreDb.CreateAsync(projectId);
                        }
                        else
                        {
                             System.Diagnostics.Debug.WriteLine("project_id not found in credentials file!");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Firebase credentials file not found!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Firestore: {ex.Message}");
            }
        }

        public async Task<List<DictionaryItem>> GetAllAsync(string collectionName)
        {
            if (_firestoreDb == null) return new List<DictionaryItem>();

            var collection = _firestoreDb.Collection(collectionName);
            // Changed to client-side filtering to support legacy documents where "IsDeleted" field is missing.
            // Server-side WhereEqualTo("IsDeleted", false) excludes missing fields.
            var snapshot = await collection.GetSnapshotAsync();
            
            var items = new List<DictionaryItem>();
            foreach (var document in snapshot.Documents)
            {
                if (document.Exists)
                {
                    var item = document.ConvertTo<DictionaryItem>();
                    item.Id = document.Id;
                    
                    // Filter out soft-deleted items (treat null as false)
                    if (!item.IsDeleted)
                    {
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        public async Task<DictionaryItem> GetByIdAsync(string collectionName, string id)
        {
            if (_firestoreDb == null) return null;

            var docRef = _firestoreDb.Collection(collectionName).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var item = snapshot.ConvertTo<DictionaryItem>();
                item.Id = snapshot.Id;
                return item;
            }
            return null;
        }

        public async Task AddAsync(string collectionName, DictionaryItem item)
        {
            if (_firestoreDb == null) return;

            // Audit
            item.CreatedAt = DateTime.UtcNow;
            item.CreatedBy = _authService.CurrentUser?.Email ?? "Unknown";
            item.ModifiedAt = DateTime.UtcNow;
            item.ModifiedBy = item.CreatedBy;
            item.IsDeleted = false;

            var collection = _firestoreDb.Collection(collectionName);
            var docRef = collection.Document(item.Id);
            await docRef.SetAsync(item);
        }

        public async Task UpdateAsync(string collectionName, DictionaryItem item)
        {
            if (_firestoreDb == null) return;

            // Audit
            item.ModifiedAt = DateTime.UtcNow;
            item.ModifiedBy = _authService.CurrentUser?.Email ?? "Unknown";

            var docRef = _firestoreDb.Collection(collectionName).Document(item.Id);
            await docRef.SetAsync(item, SetOptions.Overwrite);
        }

        public async Task DeleteAsync(string collectionName, string id)
        {
            if (_firestoreDb == null) return;

            // Soft Delete
            var docRef = _firestoreDb.Collection(collectionName).Document(id);
            var updates = new Dictionary<string, object>
            {
                { "IsDeleted", true },
                { "DeletedAt", DateTime.UtcNow },
                { "DeletedBy", _authService.CurrentUser?.Email ?? "Unknown" }
            };
            await docRef.UpdateAsync(updates);
        }

        public async Task<(List<DictionaryItem> Items, object LastDocument)> GetPaginatedAsync(string collectionName, int limit, object lastDocument = null)
        {
            if (_firestoreDb == null) return (new List<DictionaryItem>(), null);

            var collection = _firestoreDb.Collection(collectionName);
            var query = collection.OrderBy("SourceTerm").Limit(limit);

            if (lastDocument is DocumentSnapshot lastSnapshot)
            {
                query = query.StartAfter(lastSnapshot);
            }

            var snapshot = await query.GetSnapshotAsync();
            var items = new List<DictionaryItem>();
            DocumentSnapshot lastDoc = null;

            if (snapshot.Count > 0)
            {
                lastDoc = snapshot.Documents[snapshot.Count - 1];
                foreach (var document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var item = document.ConvertTo<DictionaryItem>();
                        item.Id = document.Id;
                         // Filter out soft-deleted items (treat null as false)
                        if (!item.IsDeleted)
                        {
                            items.Add(item);
                        }
                    }
                }
            }

            return (items, lastDoc);
        }

        public async Task<List<DictionaryItem>> SearchAsync(string collectionName, string query)
        {
            if (_firestoreDb == null) return new List<DictionaryItem>();
            
            var collection = _firestoreDb.Collection(collectionName);

            // Optimization: If query is empty, usually we shouldn't be here in this new flow, but safe fallback
            if (string.IsNullOrWhiteSpace(query))
            {
                // Just get first batch if empty search
               var (items, _) = await GetPaginatedAsync(collectionName, 20, null);
               return items;
            }

            // Server-side Prefix Search on SourceTerm
            // Note: This matches "Startswith". 
            // Case sensitivity depends on how data is stored. Firestore exact matches are case sensitive.
            // If we stored exact casing, this might miss "apple" if searching "App". 
            // For now, we assume user types correct case or we standardise storage later.
            // Standard approach: Use a "SourceTerm_Lower" field. But user didn't ask for schema change yet.
            // We will trust standard behaviour for now.
            
            // Note: We cannot filter IsDeleted=false AND do a range query on SourceTerm without composite index.
            // So we might fetch deleted items too and filter in memory.
            
            var endQuery = query + "\uf8ff";
            var queryRef = collection
                .WhereGreaterThanOrEqualTo("SourceTerm", query)
                .WhereLessThanOrEqualTo("SourceTerm", endQuery);

            var snapshot = await queryRef.GetSnapshotAsync();
            
            var results = new List<DictionaryItem>();
            foreach (var document in snapshot.Documents)
            {
                if (document.Exists)
                {
                     var item = document.ConvertTo<DictionaryItem>();
                     item.Id = document.Id;
                     if (!item.IsDeleted)
                     {
                         results.Add(item);
                     }
                }
            }
            return results;
        }















        public async Task AddBatchAsync(string collectionName, List<DictionaryItem> items)
        {
            if (_firestoreDb == null || items == null || !items.Any()) return;

            var collection = _firestoreDb.Collection(collectionName);
            var batchSize = 500;

            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = _firestoreDb.StartBatch();
                var batchItems = items.Skip(i).Take(batchSize);

                foreach (var item in batchItems)
                {
                    // Ensure ID
                    if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();

                    // Audit - reusing logic from AddAsync
                    item.CreatedAt = DateTime.UtcNow;
                    item.CreatedBy = _authService.CurrentUser?.Email ?? "Unknown";
                    item.ModifiedAt = DateTime.UtcNow;
                    item.ModifiedBy = item.CreatedBy;
                    item.IsDeleted = false;

                    var docRef = collection.Document(item.Id);
                    batch.Set(docRef, item);
                }
                await batch.CommitAsync();
            }
        }
    }
}
