using Zinote.Services;

namespace Zinote.Pages;

public partial class HubPage : ContentPage
{
    private readonly DataService _dataService;

    public HubPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _dataService.InitializeAsync();
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
}
