
using Zinote.Models;
using Zinote.Services;

namespace Zinote.Pages;

[QueryProperty(nameof(ItemId), "ItemId")]
[QueryProperty(nameof(CollectionName), "CollectionName")]
public partial class ItemDetailPage : ContentPage
{
    private readonly DataService _dataService;
    private DictionaryItem _item;
    private string _itemId;
    private string _collectionName = "dictionary_items";

    public string CollectionName
    {
        get => _collectionName;
        set => _collectionName = value;
    }

    public string ItemId
    {
        get => _itemId;
        set
        {
            _itemId = value;
            LoadItem(value);
        }
    }

    public ItemDetailPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
    }

    private async void LoadItem(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
             _item = await _dataService.GetByIdAsync(_collectionName, id);
            
            if (_item != null)
            {
                SourceTermEntry.Text = _item.SourceTerm;
                TargetTermEntry.Text = _item.TargetTerm;
                DefinitionEntry.Text = _item.Definition;
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourceTermEntry.Text))
        {
            await DisplayAlert("Error", "Source Term is required", "OK");
            return;
        }

        try
        {
            if (_item == null)
            {
                _item = new DictionaryItem
                {
                    SourceTerm = SourceTermEntry.Text,
                    TargetTerm = TargetTermEntry.Text,
                    Definition = DefinitionEntry.Text
                };
                await _dataService.AddAsync(_collectionName, _item);
            }
            else
            {
                _item.SourceTerm = SourceTermEntry.Text;
                _item.TargetTerm = TargetTermEntry.Text;
                _item.Definition = DefinitionEntry.Text;
                await _dataService.UpdateAsync(_collectionName, _item);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save item: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
