using Zinote.Services;

namespace Zinote.Pages;

public partial class HubPage : ContentPage
{
    private readonly DataService _dataService;

    public HubPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        UpdateLanguageLabel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _dataService.InitializeAsync();
        UpdateLanguageLabel(); // Ensure label is correct on reappearing
    }

    private async void OnHubTapped(object sender, EventArgs e)
    {
        if (sender is Element element && element is IGestureRecognizer recognizer)
        {
            // This is tricky to get CommandParameter from event args directly if not using Command
            // But we can cast sender to Frame if we know it's a Frame, or use the TappedEventArgs if available?
            // Actually, TapGestureRecognizer doesn't pass CommandParameter in EventArgs easily without Command.
            // Let's use the sender.
        }
        
        // Better approach: Get the Border and its GestureRecognizers
        if (sender is Border border && border.GestureRecognizers.Count > 0)
        {
            var tapGesture = border.GestureRecognizers[0] as TapGestureRecognizer;
            if (tapGesture != null && tapGesture.CommandParameter is string collectionName)
            {
                // Navigate to MainPage with CollectionName
                await Shell.Current.GoToAsync($"MainPage?CollectionName={collectionName}");
            }
        }
    }
    private void OnThemeClicked(object sender, EventArgs e)
    {
        Helpers.ThemeHelper.ToggleTheme();
    }
    
    private void OnLanguageClicked(object sender, EventArgs e)
    {
        var current = Helpers.LocalizationResourceManager.Instance.CurrentCulture;
        var newCulture = current.Name.StartsWith("tr") ? new System.Globalization.CultureInfo("en") : new System.Globalization.CultureInfo("tr");
        Helpers.LocalizationResourceManager.Instance.SetCulture(newCulture);
        UpdateLanguageLabel();
    }
    
    private void UpdateLanguageLabel()
    {
        if (LanguageLabel != null)
        {
            LanguageLabel.Text = Helpers.LocalizationResourceManager.Instance.CurrentCulture.TwoLetterISOLanguageName.ToUpper();
        }
    }
}
