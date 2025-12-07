using Zinote.Models;
using Zinote.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Zinote.Helpers;

namespace Zinote.Pages;

[QueryProperty(nameof(CollectionName), Constants.NavCollectionName)]
public partial class DictionaryListPage : ContentPage
{
    private readonly DataService _dataService;
    private readonly ExportService _exportService;
    private readonly ImportService _importService;
    private readonly AuthService _authService;
    private readonly AppSettings _settings;
    private string _collectionName = string.Empty; // Initialized empty, set via navigation
    private CancellationTokenSource _debounceCts;

    public string CollectionName
    {
        get => _collectionName;
        set
        {
            _collectionName = value;
            Title = value switch
            {
                "health_dictionary" => LocalizationResourceManager.Instance["HealthDict"],
                "military_dictionary" => LocalizationResourceManager.Instance["MilitaryDict"],
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace("_", " "))
            };
        }
    }

    private ObservableCollection<DictionaryItem> _items = new ObservableCollection<DictionaryItem>();
    private object _lastDocument;
    private const int _pageSize = 20;
    private bool _isLoadingMore;
    private bool _isDetailedSearch = false; // Flag to check if we are in search mode

    public DictionaryListPage(DataService dataService, ExportService exportService, ImportService importService, AuthService authService, AppSettings settings)
    {
        InitializeComponent();
        _dataService = dataService;
        _exportService = exportService;
        _importService = importService;
        _authService = authService;
        _settings = settings;
        BindingContext = this;
        
        // Initial setup
        ItemsCollectionView.ItemsSource = _items;

        LocalizationResourceManager.Instance.PropertyChanged += (sender, e) =>
        {
            // Re-trigger title update when language changes
             Title = _collectionName switch
            {
                "health_dictionary" => LocalizationResourceManager.Instance["HealthDict"],
                "military_dictionary" => LocalizationResourceManager.Instance["MilitaryDict"],
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_collectionName.Replace("_", " "))
            };
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _dataService.InitializeAsync();

        // Apply Configuration
        if (ImportButton != null) ImportButton.IsVisible = _settings.Features.EnableImport;
        if (ExportButton != null) ExportButton.IsVisible = _settings.Features.EnableExport;
        
        // Only load if empty or if needed. 
        // If we want a fresh reload every time we appear (e.g. after adding item), we can clear and load.
        if (_items.Count == 0)
        {
             await LoadDataAsync(true);
        }
        
        // Hide Buttons for Guest
        bool isGuest = _authService.CurrentUser?.IsGuest ?? false;
        if (isGuest)
        {
            if (AddButton != null) AddButton.IsVisible = false;
        }
    }

    private async Task LoadDataAsync(bool isRefresh = false)
    {
        if (isRefresh)
        {
            _items.Clear();
            _lastDocument = null;
            _isDetailedSearch = false; // Reset search mode
        }

        // If we are searching, we use SearchAsync instead of Pagination
        if (!string.IsNullOrWhiteSpace(SearchBar.Text))
        {
             await PerformSearchAsync(SearchBar.Text);
             return;
        }

        var (newItems, lastDoc) = await _dataService.GetPaginatedAsync(_collectionName, _pageSize, _lastDocument);
        _lastDocument = lastDoc;

        foreach (var item in newItems)
        {
            // Verify not already in list (optional, for safety)
            _items.Add(item);
        }
    }

    private async void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        if (_isLoadingMore || _isDetailedSearch) return; // Don't paginate if searching or already loading

        _isLoadingMore = true;
        try
        {
            if (_lastDocument != null) // Only load if there are more pages
            {
                await LoadDataAsync(false);
            }
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    private async void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            // Updated to 1000ms delay for optimization
            await Task.Delay(1000, token);
            if (!token.IsCancellationRequested)
            {
                var text = e.NewTextValue;

                if (string.IsNullOrWhiteSpace(text))
                {
                    // Reset to initial paginated state
                    await LoadDataAsync(true);
                }
                else if (text.Length >= 2) // Min 2 chars to search
                {
                    await PerformSearchAsync(text);
                }
                // If 1 char, do nothing (keep previous state)
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
    }

    private async Task PerformSearchAsync(string query)
    {
        _isDetailedSearch = true; // Block pagination while searching
        var items = await _dataService.SearchAsync(_collectionName, query);
        
        _items.Clear();
        foreach(var item in items)
        {
            _items.Add(item);
        }
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        if (_authService.CurrentUser?.IsGuest ?? false) 
        {
            await DisplayAlert("Restricted", "Guests cannot add items.", "OK");
            return; 
        }
        await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?{Constants.NavCollectionName}={_collectionName}");
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is DictionaryItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?{Constants.NavItemId}={item.Id}&{Constants.NavCollectionName}={_collectionName}");
            
            // Deselect item without breaking binding
            ItemsCollectionView.SelectedItem = null;
            
            // Refresh list to show potential updates (e.g. edited item)
            // Note: This resets pagination to page 1.
            await LoadDataAsync(true); 
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_authService.CurrentUser?.IsGuest ?? false)
        {
            await DisplayAlert("Restricted", "Guests cannot delete items.", "OK");
            return;
        }

        if (sender is Button button && button.CommandParameter is string id)
        {
            bool answer = await DisplayAlert(
                _settings.General.AppName, 
                "Are you sure you want to delete this item?", 
                LocalizationResourceManager.Instance["Yes"], 
                LocalizationResourceManager.Instance["No"]);
                
            if (answer)
            {
                await _dataService.DeleteAsync(_collectionName, id);
                await LoadDataAsync(true); // Refresh list
            }
        }
    }

    private void OnExportOptionsClicked(object sender, EventArgs e)
    {
        // Show the custom overlay
        ExportOverlay.IsVisible = true;
    }

    private void OnExportOverlayClose(object sender, EventArgs e)
    {
        // Hide the custom overlay
        ExportOverlay.IsVisible = false;
    }

    private async void OnExportFormatClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DataFormat action)
        {
            ExportOverlay.IsVisible = false;

            try
            {
                string filePath = await _exportService.ExportAsync(_collectionName, action);

                if (!string.IsNullOrEmpty(filePath))
                {
                    await DisplayAlert(_settings.General.AppName, $"Data exported to:\n{filePath}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
            }
        }
    }

    private void OnImportOptionsClicked(object sender, EventArgs e)
    {
        ImportOverlay.IsVisible = true;
    }

    private void OnImportOverlayClose(object sender, EventArgs e)
    {
        ImportOverlay.IsVisible = false;
    }

    private async void OnImportFormatClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DataFormat importType)
        {
            ImportOverlay.IsVisible = false;

            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                        { DevicePlatform.Android, new[] { "text/comma-separated-values", "text/csv" } },
                        { DevicePlatform.WinUI, new[] { ".csv" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
                    });

                bool isExcel = importType == DataFormat.BasicExcel || importType == DataFormat.MatecatExcel || importType == DataFormat.SmartcatExcel;
                bool isCsv = !isExcel; 

                var options = new PickOptions
                {
                    PickerTitle = "Select backup file",
                    FileTypes = isExcel ? FilePickerFileType.Images : customFileType // Images is wrong, need Xlsx or all
                };
                
                // Fix FileTypes for Excel
                if (isExcel)
                {
                      options.FileTypes = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.iOS, new[] { "com.microsoft.excel.xls" } }, 
                            { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
                            { DevicePlatform.WinUI, new[] { ".xlsx" } },
                            { DevicePlatform.MacCatalyst, new[] { "com.microsoft.excel.xls" } } 
                        });
                }

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    List<DictionaryItem> items = new List<DictionaryItem>();

                    if (isCsv)
                    {
                        items = _importService.ImportFromCsv(stream, importType);
                    }
                    else if (isExcel)
                    {
                        items = _importService.ImportFromExcel(stream, importType);
                    }

                    if (items.Count > 0)
                    {
                         // Confirm
                        bool confirm = await DisplayAlert(_settings.General.AppName, 
                            $"Found {items.Count} items. Do you want to import them into '{_collectionName}'?", 
                            LocalizationResourceManager.Instance["Yes"], 
                            LocalizationResourceManager.Instance["No"]);
                        
                        if (confirm)
                        {
                            await _dataService.AddBatchAsync(_collectionName, items);
                             await DisplayAlert(_settings.General.AppName, "Import successful!", "OK");
                             await LoadDataAsync(true);
                        }
                    }
                    else
                    {
                        await DisplayAlert(_settings.General.AppName, "No valid items found in the file.", "OK");
                    }
                }
            }
            catch (InvalidDataException ex)
            {
               await DisplayAlert("Validation Error", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
            }
        }
    }
}
