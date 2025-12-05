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

        public async Task<string> ExportToCsvAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SourceTerm,TargetTerm,Definition");
            foreach (var item in items)
            {
                sb.AppendLine($"{EscapeCsv(item.SourceTerm)},{EscapeCsv(item.TargetTerm)},{EscapeCsv(item.Definition)}");
            }

            var fileName = $"{collectionName}_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            return filePath;
        }

        public async Task<string> ExportToMatecatAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var sb = new System.Text.StringBuilder();
            // Matecat headers: Forbidden, Domain, Subdomain, Definition, en-US, Notes, ExampleOfUse, tr-TR
            // Mapping: en-US -> SourceTerm, tr-TR -> TargetTerm, Definition -> Definition
            sb.AppendLine("Forbidden,Domain,Subdomain,Definition,en-US,Notes,ExampleOfUse,tr-TR");

            foreach (var item in items)
            {
                // Mapping: en-US -> SourceTerm, tr-TR -> TargetTerm
                string forbiddenValue = item.Forbidden ? "true" : "";
                sb.AppendLine($"{EscapeCsv(forbiddenValue)},{EscapeCsv(item.Domain)},{EscapeCsv(item.SubDomain)},{EscapeCsv(item.Definition)},{EscapeCsv(item.SourceTerm)},{EscapeCsv(item.Notes)},{EscapeCsv(item.ExampleOfUse)},{EscapeCsv(item.TargetTerm)}");
            }

            var fileName = $"{collectionName}_MatecatExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            return filePath;
        }

        public async Task<string> ExportToMatecatExcelAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var fileName = $"{collectionName}_MatecatExport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Matecat Export");

                // Headers
                worksheet.Cell(1, 1).Value = "Forbidden";
                worksheet.Cell(1, 2).Value = "Domain";
                worksheet.Cell(1, 3).Value = "Subdomain";
                worksheet.Cell(1, 4).Value = "Definition";
                worksheet.Cell(1, 5).Value = "en-US";
                worksheet.Cell(1, 6).Value = "Notes";
                worksheet.Cell(1, 7).Value = "ExampleOfUse";
                worksheet.Cell(1, 8).Value = "tr-TR";

                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

                int row = 2;
                foreach (var item in items)
                {
                    worksheet.Cell(row, 1).Value = item.Forbidden ? "true" : "";
                    worksheet.Cell(row, 2).Value = item.Domain;
                    worksheet.Cell(row, 3).Value = item.SubDomain;
                    worksheet.Cell(row, 4).Value = item.Definition;
                    worksheet.Cell(row, 5).Value = item.SourceTerm;
                    worksheet.Cell(row, 6).Value = item.Notes;
                    worksheet.Cell(row, 7).Value = item.ExampleOfUse;
                    worksheet.Cell(row, 8).Value = item.TargetTerm;
                    row++;
                }
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
            return filePath;
        }

        public async Task<string> ExportToSmartcatCsvAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Example,Do not translate,CreationDate,Author,LastModifiedDate,LastModifiedBy,en Term1,tr Term1"); 

            foreach (var item in items)
            {
                var prohibited = item.Forbidden ? "true" : "";
                sb.AppendLine($"{EscapeCsv(item.ExampleOfUse)},{prohibited},{EscapeCsv(item.CreatedAt.ToString())},,{EscapeCsv(item.ModifiedAt.ToString())},,{EscapeCsv(item.SourceTerm)},{EscapeCsv(item.TargetTerm)}");
            }

            var fileName = $"{collectionName}_SmartcatExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            return filePath;
        }

        public async Task<string> ExportToSmartcatExcelAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var fileName = $"{collectionName}_SmartcatExport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Smartcat Export");
                worksheet.Cell(1, 1).Value = "Example";
                worksheet.Cell(1, 2).Value = "Do not translate";
                worksheet.Cell(1, 3).Value = "CreationDate";
                worksheet.Cell(1, 4).Value = "Author";
                worksheet.Cell(1, 5).Value = "LastModifiedDate";
                worksheet.Cell(1, 6).Value = "LastModifiedBy";
                worksheet.Cell(1, 7).Value = "en Term1";
                worksheet.Cell(1, 8).Value = "tr Term1";

                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;

                int row = 2;
                foreach (var item in items)
                {
                    worksheet.Cell(row, 1).Value = item.ExampleOfUse;
                    worksheet.Cell(row, 2).Value = item.Forbidden ? "true" : "";
                    worksheet.Cell(row, 3).Value = item.CreatedAt;
                    worksheet.Cell(row, 4).Value = ""; // Author
                    worksheet.Cell(row, 5).Value = item.ModifiedAt;
                    worksheet.Cell(row, 6).Value = ""; // LastModifiedBy
                    worksheet.Cell(row, 7).Value = item.SourceTerm;
                    worksheet.Cell(row, 8).Value = item.TargetTerm;
                    row++;
                }
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
            return filePath;
        }

        public async Task<string> ExportToExcelAsync(string collectionName)
        {
            var items = await GetAllAsync(collectionName);
            var fileName = $"{collectionName}_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Dictionary Items");

                // Headers
                worksheet.Cell(1, 1).Value = "Source Term";
                worksheet.Cell(1, 2).Value = "Target Term";
                worksheet.Cell(1, 3).Value = "Definition";

                // Style Headers
                var headerRange = worksheet.Range(1, 1, 1, 3);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Data
                int row = 2;
                foreach (var item in items)
                {
                    worksheet.Cell(row, 1).Value = item.SourceTerm;
                    worksheet.Cell(row, 2).Value = item.TargetTerm;
                    worksheet.Cell(row, 3).Value = item.Definition;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }

            return filePath;
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}
