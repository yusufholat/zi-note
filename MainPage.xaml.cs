
using Zinote.Models;
using Zinote.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Zinote.Helpers;

namespace Zinote;

[QueryProperty(nameof(CollectionName), Constants.NavCollectionName)]
public partial class MainPage : ContentPage
{
    private readonly DataService _dataService;
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
                "health_dictionary" => "Sağlık Sözlüğü",
                "military_dictionary" => "Askeri Sözlük",
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace("_", " "))
            };
        }
    }

    public MainPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _dataService.InitializeAsync();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var items = await _dataService.SearchAsync(_collectionName, SearchBar.Text);
        ItemsCollectionView.ItemsSource = items;
    }

    private async void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(500, token);
            if (!token.IsCancellationRequested)
            {
                await LoadDataAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?{Constants.NavCollectionName}={_collectionName}");
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is DictionaryItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?{Constants.NavItemId}={item.Id}&{Constants.NavCollectionName}={_collectionName}");
            // Deselect item
            ItemsCollectionView.SelectedItem = null;
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string id)
        {
            bool answer = await DisplayAlert("Confirm", "Are you sure you want to delete this item?", "Yes", "No");
            if (answer)
            {
                await _dataService.DeleteAsync(_collectionName, id);
                await LoadDataAsync();
            }
        }
    }

    private void OnThemeClicked(object sender, EventArgs e)
    {
        Helpers.ThemeHelper.ToggleTheme();
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
        if (sender is Button button && button.CommandParameter is string action)
        {
            // Close overlay immediately or after operation? Usually better to close immediately
            // But if there is an error, maybe keep it open?
            // Let's close it first for smoother UX
            ExportOverlay.IsVisible = false;

            try
            {
                string filePath = "";
                switch (action)
                {
                    case "Basic CSV":
                        filePath = await _dataService.ExportToCsvAsync(_collectionName);
                        break;
                    case "Basic Excel":
                        filePath = await _dataService.ExportToExcelAsync(_collectionName);
                        break;
                    case "Matecat CSV":
                        filePath = await _dataService.ExportToMatecatAsync(_collectionName);
                        break;
                    case "Matecat Excel":
                        filePath = await _dataService.ExportToMatecatExcelAsync(_collectionName);
                        break;
                    case "Smartcat CSV":
                        filePath = await _dataService.ExportToSmartcatCsvAsync(_collectionName);
                        break;
                    case "Smartcat Excel":
                        filePath = await _dataService.ExportToSmartcatExcelAsync(_collectionName);
                        break;
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    await DisplayAlert("Success", $"Data exported to:\n{filePath}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
            }
        }
    }
}
