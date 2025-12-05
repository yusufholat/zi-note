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

    private void UpdateLanguageLabel()
    {
        if (LanguageLabel != null)
        {
            LanguageLabel.Text = LocalizationResourceManager.Instance.CurrentCulture.TwoLetterISOLanguageName.ToUpper();
        }
    }
}
