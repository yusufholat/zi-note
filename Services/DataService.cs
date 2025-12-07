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
        
        // In-Memory Store: CollectionName -> List<DictionaryItem>
        private Dictionary<string, List<DictionaryItem>> _memoryStore = new Dictionary<string, List<DictionaryItem>>();

        public DataService(AuthService authService)
        {
            _authService = authService;
            // Initialize memory store with some dummy collections
            _memoryStore["health_dictionary"] = new List<DictionaryItem>();
            _memoryStore["military_dictionary"] = new List<DictionaryItem>();
        }

        public async Task InitializeAsync()
        {
            if (!Constants.UseFirebase) return;

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

        private List<DictionaryItem> GetMemoryCollection(string collectionName)
        {
            if (!_memoryStore.ContainsKey(collectionName))
            {
                _memoryStore[collectionName] = new List<DictionaryItem>();
            }
            return _memoryStore[collectionName];
        }

        public async Task<List<DictionaryItem>> GetAllAsync(string collectionName)
        {
            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                 var memItems = GetMemoryCollection(collectionName)
                                .Where(i => !i.IsDeleted)
                                .ToList();
                return await Task.FromResult(memItems);
            }

            try 
            {
                var collection = _firestoreDb.Collection(collectionName);
                var snapshot = await collection.GetSnapshotAsync();
                
                var items = new List<DictionaryItem>();
                foreach (var document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var item = document.ConvertTo<DictionaryItem>();
                        item.Id = document.Id;
                        
                        if (!item.IsDeleted)
                        {
                            items.Add(item);
                        }
                    }
                }
                return items;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (GetAll): {ex.Message}");
                return new List<DictionaryItem>();
            }
        }

        public async Task<DictionaryItem> GetByIdAsync(string collectionName, string id)
        {
            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                var item = GetMemoryCollection(collectionName).FirstOrDefault(i => i.Id == id);
                return await Task.FromResult(item);
            }

            try
            {
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
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Firestore Error (GetById): {ex.Message}");
                 return null;
            }
        }

        public async Task AddAsync(string collectionName, DictionaryItem item)
        {
            // Audit
            item.CreatedAt = DateTime.UtcNow;
            item.CreatedBy = _authService.CurrentUser?.Email ?? "Unknown";
            item.ModifiedAt = DateTime.UtcNow;
            item.ModifiedBy = item.CreatedBy;
            item.IsDeleted = false;

            if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();

            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                GetMemoryCollection(collectionName).Add(item);
                await Task.CompletedTask;
                return;
            }

            try
            {
                var collection = _firestoreDb.Collection(collectionName);
                var docRef = collection.Document(item.Id);
                await docRef.SetAsync(item);
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Firestore Error (Add): {ex.Message}");
            }
        }

        public async Task UpdateAsync(string collectionName, DictionaryItem item)
        {
            // Audit
            item.ModifiedAt = DateTime.UtcNow;
            item.ModifiedBy = _authService.CurrentUser?.Email ?? "Unknown";

            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                var list = GetMemoryCollection(collectionName);
                var existing = list.FirstOrDefault(i => i.Id == item.Id);
                if (existing != null)
                {
                    // Replace
                    list.Remove(existing);
                    list.Add(item);
                }
                await Task.CompletedTask;
                return;
            }

            try
            {
                var docRef = _firestoreDb.Collection(collectionName).Document(item.Id);
                await docRef.SetAsync(item, SetOptions.Overwrite);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (Update): {ex.Message}");
            }
        }

        public async Task DeleteAsync(string collectionName, string id)
        {
            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                var list = GetMemoryCollection(collectionName);
                var existing = list.FirstOrDefault(i => i.Id == id);
                if (existing != null)
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = DateTime.UtcNow;
                    existing.DeletedBy = _authService.CurrentUser?.Email ?? "Unknown";
                }
                await Task.CompletedTask;
                return;
            }

            try
            {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (Delete): {ex.Message}");
            }
        }

        public async Task<(List<DictionaryItem> Items, object LastDocument)> GetPaginatedAsync(string collectionName, int limit, object lastDocument = null)
        {
             if (!Constants.UseFirebase || _firestoreDb == null)
            {
                // Mimic pagination in memory
                var list = GetMemoryCollection(collectionName)
                            .Where(i => !i.IsDeleted)
                            .OrderBy(i => i.SourceTerm)
                            .ToList();
                
                int startIndex = 0;
                if (lastDocument is int lastIndex)
                {
                    startIndex = lastIndex + 1;
                }

                var pagedItems = list.Skip(startIndex).Take(limit).ToList();
                object nextLastDoc = pagedItems.Any() ? (startIndex + pagedItems.Count - 1) : null;
                
                return await Task.FromResult((pagedItems, nextLastDoc));
            }

            try
            {
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
                            // Filter out soft-deleted items
                            if (!item.IsDeleted)
                            {
                                items.Add(item);
                            }
                        }
                    }
                }

                return (items, lastDoc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (GetPaginated): {ex.Message}");
                return (new List<DictionaryItem>(), null);
            }
        }

        public async Task<List<DictionaryItem>> SearchAsync(string collectionName, string query)
        {
            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                 if (string.IsNullOrWhiteSpace(query))
                {
                    var (all, _) = await GetPaginatedAsync(collectionName, 20, null);
                    return all;
                }
                
                return GetMemoryCollection(collectionName)
                        .Where(i => !i.IsDeleted && i.SourceTerm != null && i.SourceTerm.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
            }
            
            try
            {
                var collection = _firestoreDb.Collection(collectionName);

                // Optimization: If query is empty, usually we shouldn't be here in this new flow, but safe fallback
                if (string.IsNullOrWhiteSpace(query))
                {
                    // Just get first batch if empty search
                    var (items, _) = await GetPaginatedAsync(collectionName, 20, null);
                    return items;
                }
                
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (Search): {ex.Message}");
                return new List<DictionaryItem>();
            }
        }















        public async Task AddBatchAsync(string collectionName, List<DictionaryItem> items)
        {
            if (items == null || !items.Any()) return;

            // Prepare items
             foreach (var item in items)
             {
                 if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();
                 item.CreatedAt = DateTime.UtcNow;
                 item.CreatedBy = _authService.CurrentUser?.Email ?? "Unknown";
                 item.ModifiedAt = DateTime.UtcNow;
                 item.ModifiedBy = item.CreatedBy;
                 item.IsDeleted = false;
             }
             
            if (!Constants.UseFirebase || _firestoreDb == null)
            {
                GetMemoryCollection(collectionName).AddRange(items);
                await Task.CompletedTask;
                return;
            }

            try
            {
                var collection = _firestoreDb.Collection(collectionName);
                var batchSize = 500;

                for (int i = 0; i < items.Count; i += batchSize)
                {
                    var batch = _firestoreDb.StartBatch();
                    var batchItems = items.Skip(i).Take(batchSize);

                    foreach (var item in batchItems)
                    {
                        var docRef = collection.Document(item.Id);
                        batch.Set(docRef, item);
                    }
                    await batch.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Firestore Error (AddBatch): {ex.Message}");
            }
        }
    }
}
