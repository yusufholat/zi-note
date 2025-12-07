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
        private readonly AppSettings _settings;
        
        // In-Memory Store: CollectionName -> List<DictionaryItem>
        private Dictionary<string, List<DictionaryItem>> _memoryStore = new Dictionary<string, List<DictionaryItem>>();

        // Cache for Firestore items: CollectionName -> (ItemId -> DictionaryItem)
        private Dictionary<string, Dictionary<string, DictionaryItem>> _itemCache = new Dictionary<string, Dictionary<string, DictionaryItem>>();

        // Track fully loaded collections
        private HashSet<string> _fullyLoadedCollections = new HashSet<string>();

        // Lock for thread safety
        private readonly object _cacheLock = new object();

        public DataService(AuthService authService, AppSettings settings)
        {
            _authService = authService;
            _settings = settings;
            // Initialize memory store with some dummy collections
            _memoryStore["health_dictionary"] = new List<DictionaryItem>();
            _memoryStore["military_dictionary"] = new List<DictionaryItem>();
        }

        public async Task InitializeAsync()
        {
            if (!_settings.Integration.UseFirebase) return;
            if (_firestoreDb != null) return; // Optimization: Skip if already initialized

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
            lock (_cacheLock)
            {
                if (!_memoryStore.ContainsKey(collectionName))
                {
                    _memoryStore[collectionName] = new List<DictionaryItem>();
                }
                return _memoryStore[collectionName];
            }
        }

        private void CacheItem(string collectionName, DictionaryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id)) return;

            lock (_cacheLock)
            {
                if (!_itemCache.ContainsKey(collectionName))
                {
                    _itemCache[collectionName] = new Dictionary<string, DictionaryItem>();
                }
                _itemCache[collectionName][item.Id] = item;
            }
        }

        private DictionaryItem GetCachedItem(string collectionName, string id)
        {
            lock (_cacheLock)
            {
                if (_itemCache.ContainsKey(collectionName) && _itemCache[collectionName].TryGetValue(id, out var item))
                {
                    return item;
                }
            }
            return null;
        }

        private void RemoveCachedItem(string collectionName, string id)
        {
            lock (_cacheLock)
            {
                if (_itemCache.ContainsKey(collectionName))
                {
                    _itemCache[collectionName].Remove(id);
                }
            }
        }

        private void UpdateCachedItem(string collectionName, DictionaryItem freshItem)
        {
            lock (_cacheLock)
            {
                 if (!_itemCache.ContainsKey(collectionName))
                {
                    _itemCache[collectionName] = new Dictionary<string, DictionaryItem>();
                }
                
                if (_itemCache[collectionName].TryGetValue(freshItem.Id, out var existing))
                {
                    // Update properties of existing instance to preserve references
                    existing.SourceTerm = freshItem.SourceTerm;
                    existing.TargetTerm = freshItem.TargetTerm;
                    existing.Definition = freshItem.Definition;
                    existing.Domain = freshItem.Domain;
                    existing.SubDomain = freshItem.SubDomain;
                    existing.Notes = freshItem.Notes;
                    existing.ExampleOfUse = freshItem.ExampleOfUse;
                    existing.Forbidden = freshItem.Forbidden;
                    existing.ModifiedAt = freshItem.ModifiedAt;
                    existing.ModifiedBy = freshItem.ModifiedBy;
                    existing.CreatedAt = freshItem.CreatedAt;
                    existing.CreatedBy = freshItem.CreatedBy;
                    existing.IsDeleted = freshItem.IsDeleted;
                    existing.DeletedAt = freshItem.DeletedAt;
                    existing.DeletedBy = freshItem.DeletedBy;
                }
                else
                {
                    _itemCache[collectionName][freshItem.Id] = freshItem;
                }
            }
        }

        private void MarkCachedItemDeleted(string collectionName, string id)
        {
            lock (_cacheLock)
            {
                if (_itemCache.ContainsKey(collectionName) && _itemCache[collectionName].TryGetValue(id, out var item))
                {
                    item.IsDeleted = true;
                    item.DeletedAt = DateTime.UtcNow;
                    item.DeletedBy = _authService.CurrentUser?.Email ?? "Unknown";
                }
            }
        }

        private void CacheItemsBatch(string collectionName, IEnumerable<DictionaryItem> items)
        {
             lock (_cacheLock)
            {
                if (!_itemCache.ContainsKey(collectionName))
                {
                    _itemCache[collectionName] = new Dictionary<string, DictionaryItem>();
                }
                foreach (var item in items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.Id))
                    {
                        _itemCache[collectionName][item.Id] = item;
                    }
                }
            }
        }

        public async Task LoadFullCollectionAsync(string collectionName)
        {
            lock (_cacheLock)
            {
                if (_fullyLoadedCollections.Contains(collectionName)) return;
            }

            // This loads all and caches them using GetAllAsync (which manages the lock/flag)
            await GetAllAsync(collectionName);
            
            lock (_cacheLock)
            {
                _fullyLoadedCollections.Add(collectionName);
            }
        }

        public async Task ForceReloadCollectionAsync(string collectionName)
        {
            lock (_cacheLock)
            {
                // Clear state to force reload
                if (_fullyLoadedCollections.Contains(collectionName))
                {
                    _fullyLoadedCollections.Remove(collectionName);
                }
                if (_itemCache.ContainsKey(collectionName))
                {
                    _itemCache[collectionName].Clear();
                }
            }
            // Load fresh
            await LoadFullCollectionAsync(collectionName);
        }

        public async Task<List<DictionaryItem>> GetAllAsync(string collectionName)
        {
            lock (_cacheLock)
            {
                // If already fully loaded, return from cache
                if (_fullyLoadedCollections.Contains(collectionName) && _itemCache.ContainsKey(collectionName))
                {
                    return _itemCache[collectionName].Values
                        .Where(i => !i.IsDeleted)
                        .OrderBy(i => i.SourceTerm)
                        .ToList();
                }
            }

            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
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
                        var freshItem = document.ConvertTo<DictionaryItem>();
                        freshItem.Id = document.Id;
                        
                        // Update cache
                        UpdateCachedItem(collectionName, freshItem);
                        
                        // Use cached item
                        var item = GetCachedItem(collectionName, freshItem.Id);

                        if (!item.IsDeleted)
                        {
                            items.Add(item);
                        }
                    }
                }
                
                lock (_cacheLock)
                {
                    _fullyLoadedCollections.Add(collectionName);
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
            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
            {
                var item = GetMemoryCollection(collectionName).FirstOrDefault(i => i.Id == id);
                return await Task.FromResult(item);
            }

             // Check Cache First
            var cached = GetCachedItem(collectionName, id);
            if (cached != null)
            {
                return cached;
            }

            try
            {
                var docRef = _firestoreDb.Collection(collectionName).Document(id);
                var snapshot = await docRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    var freshItem = snapshot.ConvertTo<DictionaryItem>();
                    freshItem.Id = snapshot.Id;
                    
                    CacheItem(collectionName, freshItem);
                    return freshItem;
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

            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
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
                
                // Add to cache
                CacheItem(collectionName, item);
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

            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
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
                
                // Update Cache
                UpdateCachedItem(collectionName, item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (Update): {ex.Message}");
            }
        }

        public async Task DeleteAsync(string collectionName, string id)
        {
            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
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
                
                // Remove from cache or mark as deleted
                MarkCachedItemDeleted(collectionName, id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firestore Error (Delete): {ex.Message}");
            }
        }

        public async Task<(List<DictionaryItem> Items, object LastDocument)> GetPaginatedAsync(string collectionName, int limit, object lastDocument = null)
        {
            bool useMemory = !_settings.Integration.UseFirebase || _firestoreDb == null;
            bool isFullyLoaded = false;
            
            lock (_cacheLock)
            {
                isFullyLoaded = _fullyLoadedCollections.Contains(collectionName);
            }

             if (useMemory || isFullyLoaded)
            {
                List<DictionaryItem> list;
                lock(_cacheLock)
                {
                    // Get from cache/memory safely inside lock
                    IEnumerable<DictionaryItem> source = useMemory ? GetMemoryCollection(collectionName) : _itemCache[collectionName].Values;
                    list = source
                            .Where(i => !i.IsDeleted)
                            .OrderBy(i => i.SourceTerm)
                            .ToList();
                }
                
                int startIndex = 0;
                if (lastDocument is int lastIndex)
                {
                    startIndex = lastIndex + 1;
                }
                else if (lastDocument is DocumentSnapshot lastSnap) // Transition handling
                {
                    // We switched from Firestore to Memory mid-pagination.
                    // Try to find the last item in the full sorted list to resume
                    var lastId = lastSnap.Id;
                    var foundIndex = list.FindIndex(i => i.Id == lastId);
                    if (foundIndex >= 0)
                    {
                        startIndex = foundIndex + 1;
                    }
                    else
                    {
                        // Fallback: This is rare (maybe item deleted or not in cache yet?), reset to 0
                        startIndex = 0; 
                    }
                }

                var pagedItems = list.Skip(startIndex).Take(limit).ToList();
                object nextLastDoc = pagedItems.Any() ? (startIndex + pagedItems.Count - 1) : null;
                
                return await Task.FromResult((pagedItems, nextLastDoc));
            }

            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
            {
                // This block is now covered above but kept for structure mirroring if firestore fails
                 return await Task.FromResult<(List<DictionaryItem>, object)>((new List<DictionaryItem>(), null));
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
                            var freshItem = document.ConvertTo<DictionaryItem>();
                            freshItem.Id = document.Id;
                            
                            // Update cache and use cached instance
                            UpdateCachedItem(collectionName, freshItem);
                            var item = GetCachedItem(collectionName, freshItem.Id);

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
            if (string.IsNullOrWhiteSpace(query))
            {
                var (all, _) = await GetPaginatedAsync(collectionName, 20, null);
                return all;
            }

            bool useFirebase = _settings.Integration.UseFirebase && _firestoreDb != null;
            bool isFullyLoaded = false;
            
            lock(_cacheLock)
            {
                 isFullyLoaded = _fullyLoadedCollections.Contains(collectionName);
            }
            
            // Optimization: If collection is fully loaded, search memory.
            
            if (!useFirebase || isFullyLoaded)
            {
                List<DictionaryItem> results;
                lock (_cacheLock)
                {
                    IEnumerable<DictionaryItem> source = !useFirebase ? GetMemoryCollection(collectionName) : _itemCache[collectionName].Values;
                    
                    // Case Insensitive Search (Flexible)
                    results = source
                            .Where(i => !i.IsDeleted && i.SourceTerm != null && i.SourceTerm.Contains(query, StringComparison.OrdinalIgnoreCase)) 
                            .ToList();
                }
                return results;
            }
            
            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
            {
                 // Fallback for memory store covered above
                 return new List<DictionaryItem>();
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
                        var freshItem = document.ConvertTo<DictionaryItem>();
                        freshItem.Id = document.Id;
                        
                        UpdateCachedItem(collectionName, freshItem);
                        var item = GetCachedItem(collectionName, freshItem.Id);

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
             
            if (!_settings.Integration.UseFirebase || _firestoreDb == null)
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

                    // Update Cache
                    CacheItemsBatch(collectionName, batchItems);
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Firestore Error (AddBatch): {ex.Message}");
            }
        }
    }
}
