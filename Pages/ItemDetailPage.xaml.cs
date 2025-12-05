
using Zinote.Models;
using Zinote.Services;
using Zinote.Helpers;

namespace Zinote.Pages;

[QueryProperty(nameof(ItemId), Constants.NavItemId)]
[QueryProperty(nameof(CollectionName), Constants.NavCollectionName)]
public partial class ItemDetailPage : ContentPage, IQueryAttributable
{
    private readonly DataService _dataService;
    private DictionaryItem _item;
    private string _itemId;
    private string _collectionName = string.Empty;

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
        if (query.TryGetValue(Constants.NavCollectionName, out var collectionName))
        {
            _collectionName = Uri.UnescapeDataString(collectionName.ToString());
        }

        if (query.TryGetValue(Constants.NavItemId, out var itemId))
        {
            _itemId = Uri.UnescapeDataString(itemId.ToString());
            LoadItem(_itemId);
        }
        else
        {
            // New item, default to "Hayır" (No)
            // Setting IsToggled triggers the event which updates the label
            Dispatcher.Dispatch(() => 
            {
               if (ForbiddenSwitch != null) ForbiddenSwitch.IsToggled = false;
               if (ForbiddenStateLabel != null) ForbiddenStateLabel.Text = LocalizationResourceManager.Instance["No"];
            });
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
                    // Label updates automatically via event, but to be sure:
                    ForbiddenStateLabel.Text = _item.Forbidden ? LocalizationResourceManager.Instance["Yes"] : LocalizationResourceManager.Instance["No"];
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

    private void OnThemeClicked(object sender, EventArgs e)
    {
        Helpers.ThemeHelper.ToggleTheme();
    }
    
    private void OnLanguageClicked(object sender, EventArgs e)
    {
        var current = LocalizationResourceManager.Instance.CurrentCulture;
        var newCulture = current.Name.StartsWith("tr") ? new System.Globalization.CultureInfo("en") : new System.Globalization.CultureInfo("tr");
        LocalizationResourceManager.Instance.SetCulture(newCulture);
        UpdateLanguageLabel();
    }
    
    private void UpdateLanguageLabel()
    {
        if (LanguageLabel != null)
        {
            LanguageLabel.Text = LocalizationResourceManager.Instance.CurrentCulture.TwoLetterISOLanguageName.ToUpper();
        }
        
        // Also update Forbidden label if state exists
         if (ForbiddenStateLabel != null && ForbiddenSwitch != null)
        {
             ForbiddenStateLabel.Text = ForbiddenSwitch.IsToggled ? LocalizationResourceManager.Instance["Yes"] : LocalizationResourceManager.Instance["No"];
        }
    }

    private void OnForbiddenToggled(object sender, ToggledEventArgs e)
    {
        if (ForbiddenStateLabel != null)
        {
            ForbiddenStateLabel.Text = e.Value ? LocalizationResourceManager.Instance["Yes"] : LocalizationResourceManager.Instance["No"];
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
