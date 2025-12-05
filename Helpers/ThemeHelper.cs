using Microsoft.Maui.Controls;

namespace Zinote.Helpers
{
    public static class ThemeHelper
    {
        public static bool IsDarkTheme { get; private set; } = true;

        public static void ToggleTheme()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    IsDarkTheme = !IsDarkTheme;

                    // Update UserAppTheme
                    App.Current.UserAppTheme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;
                    
                    // Swap ResourceDictionary
                    ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
                    if (mergedDictionaries != null)
                    {
                        // Create new theme
                        var newThemeSource = IsDarkTheme 
                            ? "Resources/Styles/ThemeDark.xaml" 
                            : "Resources/Styles/ThemeLight.xaml";
                        
                        var newTheme = new ResourceDictionary { Source = new Uri(newThemeSource, UriKind.Relative) };

                        // Add new theme FIRST (to ensure resources exist)
                        mergedDictionaries.Add(newTheme);

                        // Find old theme(s) by checking for a known key
                        // This handles cases where Source path might be null or different at runtime
                        var oldThemes = mergedDictionaries.Where(d => 
                            d != newTheme && 
                            d.ContainsKey("DynamicPageBackground")).ToList();

                        // Remove old theme(s)
                        foreach (var oldTheme in oldThemes)
                        {
                            mergedDictionaries.Remove(oldTheme);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Theme toggle error: {ex.Message}");
                }
            });
        }
    }
}
