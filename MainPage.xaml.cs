
using Zinote.Models;
using Zinote.Services;
using System.Collections.ObjectModel;

namespace Zinote;

[QueryProperty(nameof(CollectionName), "CollectionName")]
public partial class MainPage : ContentPage
{
    private readonly DataService _dataService;
    private string _collectionName = "dictionary_items"; // Default

    public string CollectionName
    {
        get => _collectionName;
        set
        {
            _collectionName = value;
            Title = $"{char.ToUpper(value[0]) + value.Substring(1)} Manager";
            // Reload data when collection changes
            _ = LoadDataAsync();
        }
    }

    public MainPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
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
        await LoadDataAsync();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?CollectionName={_collectionName}");
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is DictionaryItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(Pages.ItemDetailPage)}?ItemId={item.Id}&CollectionName={_collectionName}");
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

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var filePath = await _dataService.ExportToCsvAsync(_collectionName);
            await DisplayAlert("Success", $"Data exported to:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
        }
    }

    private async void OnExportMatecatClicked(object sender, EventArgs e)
    {
        try
        {
            var filePath = await _dataService.ExportToMatecatAsync(_collectionName);
            await DisplayAlert("Success", $"Matecat data exported to:\n{filePath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
        }
    }
}
