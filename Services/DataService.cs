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

        public DataService()
        {
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
            var snapshot = await collection.GetSnapshotAsync();
            
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

            var collection = _firestoreDb.Collection(collectionName);
            var docRef = collection.Document(item.Id);
            await docRef.SetAsync(item);
        }

        public async Task UpdateAsync(string collectionName, DictionaryItem item)
        {
            if (_firestoreDb == null) return;

            var docRef = _firestoreDb.Collection(collectionName).Document(item.Id);
            await docRef.SetAsync(item, SetOptions.Overwrite);
        }

        public async Task DeleteAsync(string collectionName, string id)
        {
            if (_firestoreDb == null) return;

            var docRef = _firestoreDb.Collection(collectionName).Document(id);
            await docRef.DeleteAsync();
        }

        public async Task<List<DictionaryItem>> SearchAsync(string collectionName, string query)
        {
            var allItems = await GetAllAsync(collectionName);

            if (string.IsNullOrWhiteSpace(query))
                return allItems;

            return allItems.Where(x => 
                (x.SourceTerm?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) || 
                (x.TargetTerm?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Definition?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }














    }
}
