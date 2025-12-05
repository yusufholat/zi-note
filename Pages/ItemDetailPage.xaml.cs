
using Zinote.Models;
using Zinote.Services;

namespace Zinote.Pages;

[QueryProperty(nameof(ItemId), "ItemId")]
[QueryProperty(nameof(CollectionName), "CollectionName")]
public partial class ItemDetailPage : ContentPage, IQueryAttributable
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
        set => _itemId = value;
    }

    public ItemDetailPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("CollectionName", out var collectionName))
        {
            _collectionName = Uri.UnescapeDataString(collectionName.ToString());
        }

        if (query.TryGetValue("ItemId", out var itemId))
        {
            _itemId = Uri.UnescapeDataString(itemId.ToString());
            LoadItem(_itemId);
        }
    }

    private async void LoadItem(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        try
        {
            _item = await _dataService.GetByIdAsync(_collectionName, id);
            
            if (_item != null)
            {
                Dispatcher.Dispatch(() =>
                {
                    SourceTermEntry.Text = _item.SourceTerm;
                    TargetTermEntry.Text = _item.TargetTerm;
                    DefinitionEntry.Text = _item.Definition;
                    DomainEntry.Text = _item.Domain;
                    SubDomainEntry.Text = _item.SubDomain;
                    NotesEntry.Text = _item.Notes;
                    ExampleEntry.Text = _item.ExampleOfUse;
                    ForbiddenSwitch.IsToggled = _item.Forbidden;
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load item: {ex.Message}", "OK");
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
                    Definition = DefinitionEntry.Text,
                    Domain = DomainEntry.Text,
                    SubDomain = SubDomainEntry.Text,
                    Notes = NotesEntry.Text,
                    ExampleOfUse = ExampleEntry.Text,
                    Forbidden = ForbiddenSwitch.IsToggled,
                    ModifiedAt = DateTime.UtcNow
                };
                await _dataService.AddAsync(_collectionName, _item);
            }
            else
            {
                _item.SourceTerm = SourceTermEntry.Text;
                _item.TargetTerm = TargetTermEntry.Text;
                _item.Definition = DefinitionEntry.Text;
                _item.Domain = DomainEntry.Text;
                _item.SubDomain = SubDomainEntry.Text;
                _item.Notes = NotesEntry.Text;
                _item.ExampleOfUse = ExampleEntry.Text;
                _item.Forbidden = ForbiddenSwitch.IsToggled;
                _item.ModifiedAt = DateTime.UtcNow;
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
