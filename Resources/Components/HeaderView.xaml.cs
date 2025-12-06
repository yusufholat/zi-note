using Zinote.Helpers;

namespace Zinote.Resources.Components;

public partial class HeaderView : ContentView
{
    public static readonly BindableProperty TitleTextProperty =
        BindableProperty.Create(nameof(TitleText), typeof(string), typeof(HeaderView), string.Empty);

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public HeaderView()
    {
        InitializeComponent();
        UpdateLanguageLabel();
    }
    
    private void OnLanguageClicked(object sender, EventArgs e)
    {
        var current = LocalizationResourceManager.Instance.CurrentCulture;
        var newCulture = current.Name.StartsWith("tr") ? new System.Globalization.CultureInfo("en") : new System.Globalization.CultureInfo("tr");
        LocalizationResourceManager.Instance.SetCulture(newCulture);
        UpdateLanguageLabel();
    }
    
    private void OnThemeClicked(object sender, EventArgs e)
    {
        ThemeHelper.ToggleTheme();
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        try 
        {
            var authService = Handler?.MauiContext?.Services.GetService<Services.AuthService>();
            if (authService == null) return;

            if (authService.CurrentUser == null)
            {
                await authService.CheckLoginStatusAsync();
            }

            var email = authService.CurrentUser?.Email ?? "User";
            
            // Should verify if we can display alerts from ContentView. 
            // We usually need a Page reference for DisplayActionSheet.
            // Using Application.Current.MainPage is technically deprecated but workable, or traversing parents.
            // Shell.Current.CurrentPage is a better bet in Shell apps.

            var page = Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
            if (page == null) return;

            string action = await page.DisplayActionSheet($"Hi, {email}", "Cancel", null, "Logout");

            if (action == "Logout")
            {
                authService.SignOut();
                Application.Current.MainPage = new Pages.LoginPage(authService);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Profile error: {ex.Message}");
        }
    }

    private void UpdateLanguageLabel()
    {
        if (LanguageLabel != null)
        {
            LanguageLabel.Text = LocalizationResourceManager.Instance.CurrentCulture.TwoLetterISOLanguageName.ToUpper();
        }
    }
}
