namespace Zinote.Models
{
    public class AppSettings
    {
        public GeneralSettings General { get; init; } = new();
        public IntegrationSettings Integration { get; init; } = new();
        public FeatureSettings Features { get; init; } = new();
    }

    public class GeneralSettings
    {
        public string AppName { get; init; } = string.Empty;
    }

    public class IntegrationSettings
    {
        public bool UseFirebase { get; init; }
    }

    public class FeatureSettings
    {
        public bool EnableImport { get; init; }
        public bool EnableExport { get; init; }
    }
}
