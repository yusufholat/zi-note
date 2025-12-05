using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Zinote.Helpers
{
    public class LocalizationResourceManager : INotifyPropertyChanged
    {
        private static LocalizationResourceManager _instance;
        public static LocalizationResourceManager Instance => _instance ??= new LocalizationResourceManager();

        private readonly ResourceManager _resourceManager;
        private CultureInfo _currentCulture = new CultureInfo("en"); // Default to English initially

        public LocalizationResourceManager()
        {
            // Ensure the base name matches the path to the resx file in the assembly
            // Namespace.Folder.filename
            _resourceManager = new ResourceManager("Zinote.Resources.Languages.AppResources", typeof(LocalizationResourceManager).Assembly);
        }

        public string this[string key]
        {
            get
            {
                try
                {
                    var text = _resourceManager.GetString(key, _currentCulture);
                    return text ?? key;
                }
                catch
                {
                    return key; // Fallback to key if issues
                }
            }
        }

        public void SetCulture(CultureInfo culture)
        {
            _currentCulture = culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); // Refresh all bindings
        }
        
        public CultureInfo CurrentCulture => _currentCulture;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
