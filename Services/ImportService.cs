using ClosedXML.Excel;
using Zinote.Models;
using Zinote.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace Zinote.Services
{
    public class ImportService
    {
        public List<DictionaryItem> ImportFromCsv(Stream stream, string importType)
        {
            var items = new List<DictionaryItem>();
            using (var reader = new StreamReader(stream))
            {
                string headerLine = reader.ReadLine(); 
                ValidateCsvHeader(headerLine, importType);
                
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = ParseCsvLine(line);
                    var item = new DictionaryItem();

                    if (importType == Constants.TypeBasicCsv)
                    {
                        if (parts.Count >= 3)
                        {
                            item.SourceTerm = parts[0];
                            item.TargetTerm = parts[1];
                            item.Definition = parts[2];
                        }
                    }
                    else if (importType == Constants.TypeMatecatCsv)
                    {
                        // Forbidden,Domain,Subdomain,Definition,en-US,Notes,ExampleOfUse,tr-TR
                        if (parts.Count >= 8)
                        {
                            item.Forbidden = parts[0].ToLower() == "true";
                            item.Domain = parts[1];
                            item.SubDomain = parts[2];
                            item.Definition = parts[3];
                            item.SourceTerm = parts[4]; // en-US
                            item.Notes = parts[5];
                            item.ExampleOfUse = parts[6];
                            item.TargetTerm = parts[7]; // tr-TR
                        }
                    }
                    else if (importType == Constants.TypeSmartcatCsv)
                    {
                        // Example,Do not translate,CreationDate,Author,LastModifiedDate,LastModifiedBy,en Term1,tr Term1
                         if (parts.Count >= 8)
                        {
                            item.ExampleOfUse = parts[0];
                            item.Forbidden = parts[1].ToLower() == "true";
                            // Date parsing/Author ignored
                            item.SourceTerm = parts[6];
                            item.TargetTerm = parts[7];
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(item.SourceTerm) || !string.IsNullOrWhiteSpace(item.TargetTerm))
                    {
                         items.Add(item);
                    }
                }
            }
            return items;
        }

        public List<DictionaryItem> ImportFromExcel(Stream stream, string importType)
        {
             var items = new List<DictionaryItem>();
             using (var workbook = new XLWorkbook(stream))
             {
                 var worksheet = workbook.Worksheet(1);
                 ValidateExcelHeader(worksheet, importType);

                 var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

                 foreach (var row in rows)
                 {
                     var item = new DictionaryItem();
                     
                     if (importType == Constants.TypeBasicExcel)
                     {
                         // 1:Source, 2:Target, 3:Definition
                         item.SourceTerm = row.Cell(1).GetValue<string>();
                         item.TargetTerm = row.Cell(2).GetValue<string>();
                         item.Definition = row.Cell(3).GetValue<string>();
                     }
                      else if (importType == Constants.TypeMatecatExcel)
                     {
                         // 1:Forbidden, 2:Domain, 3:Subdomain, 4:Def, 5:en-US, 6:Notes, 7:Ex, 8:tr-TR
                         item.Forbidden = row.Cell(1).GetValue<string>().ToLower() == "true";
                         item.Domain = row.Cell(2).GetValue<string>();
                         item.SubDomain = row.Cell(3).GetValue<string>();
                         item.Definition = row.Cell(4).GetValue<string>();
                         item.SourceTerm = row.Cell(5).GetValue<string>();
                         item.Notes = row.Cell(6).GetValue<string>();
                         item.ExampleOfUse = row.Cell(7).GetValue<string>();
                         item.TargetTerm = row.Cell(8).GetValue<string>();
                     }
                     else if (importType == Constants.TypeSmartcatExcel)
                     {
                         // 1:Ex, 2:Forbidden, ... 7:en, 8:tr
                         item.ExampleOfUse = row.Cell(1).GetValue<string>();
                         item.Forbidden = row.Cell(2).GetValue<string>().ToLower() == "true";
                         item.SourceTerm = row.Cell(7).GetValue<string>();
                         item.TargetTerm = row.Cell(8).GetValue<string>();
                     }
                     
                      if (!string.IsNullOrWhiteSpace(item.SourceTerm) || !string.IsNullOrWhiteSpace(item.TargetTerm))
                    {
                         items.Add(item);
                    }
                 }
             }
             return items;
        }

        private void ValidateCsvHeader(string headerLine, string importType)
        {
            if (string.IsNullOrWhiteSpace(headerLine)) throw new InvalidDataException("CSV file is empty or missing headers.");

            string expected = string.Empty;
            switch(importType)
            {
                case Constants.TypeBasicCsv:
                    expected = "SourceTerm,TargetTerm,Definition";
                    break;
                case Constants.TypeMatecatCsv:
                    expected = "Forbidden,Domain,Subdomain,Definition,en-US,Notes,ExampleOfUse,tr-TR";
                    break;
                case Constants.TypeSmartcatCsv:
                    expected = "Example,Do not translate,CreationDate,Author,LastModifiedDate,LastModifiedBy,en Term1,tr Term1";
                    break;
            }

            if (!string.IsNullOrEmpty(expected) && !headerLine.Trim().StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Invalid CSV Format. Expected headers: {expected}");
            }
        }

        private void ValidateExcelHeader(IXLWorksheet worksheet, string importType)
        {
            var headerRow = worksheet.Row(1);
            if (headerRow.IsEmpty()) throw new InvalidDataException("Excel file is missing headers.");

            string[] expectedHeaders = null;

            switch (importType)
            {
                case Constants.TypeBasicExcel:
                    expectedHeaders = new[] { "Source Term", "Target Term", "Definition" };
                    break;
                case Constants.TypeMatecatExcel:
                    expectedHeaders = new[] { "Forbidden", "Domain", "Subdomain", "Definition", "en-US", "Notes", "ExampleOfUse", "tr-TR" };
                    break;
                case Constants.TypeSmartcatExcel:
                    expectedHeaders = new[] { "Example", "Do not translate", "CreationDate", "Author", "LastModifiedDate", "LastModifiedBy", "en Term1", "tr Term1" };
                    break;
            }

            if (expectedHeaders != null)
            {
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    string cellValue = headerRow.Cell(i + 1).GetValue<string>();
                    if (!string.Equals(cellValue?.Trim(), expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        var expectedString = string.Join(", ", expectedHeaders);
                        throw new InvalidDataException($"Invalid Excel Format. Expected headers: {expectedString}");
                    }
                }
            }
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') // Escaped quote
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString());
            return result;
        }
    }
}
