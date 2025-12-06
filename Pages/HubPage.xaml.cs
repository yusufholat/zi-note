using Zinote.Services;

namespace Zinote.Pages;

public partial class HubPage : ContentPage
{
    private readonly DataService _dataService;
    private readonly AuthService _authService;

    public HubPage(DataService dataService, AuthService authService)
    {
        InitializeComponent();
        _dataService = dataService;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _dataService.InitializeAsync();
    }

    private async void OnHubTapped(object sender, EventArgs e)
    {
        // Better approach: Get the Border and its GestureRecognizers
        if (sender is Border border && border.GestureRecognizers.Count > 0)
        {
            var tapGesture = border.GestureRecognizers[0] as TapGestureRecognizer;
            if (tapGesture != null && tapGesture.CommandParameter is string collectionName)
            {
                // Navigate to MainPage with CollectionName
                await Shell.Current.GoToAsync($"DictionaryListPage?CollectionName={collectionName}");
            }
        }
    }

}
