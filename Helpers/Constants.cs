namespace Zinote.Helpers
{
    public static class Constants
    {
        public const string CredentialsFileName = "firebase-credentials.json";

        // Navigation Parameters
        public const string NavItemId = "ItemId";
        public const string NavCollectionName = "CollectionName";
        public const string MsgUpdateItem = "UpdateItem";

        // Configuration
        // Moved to AppSettings (appsettings.json)
    }

    // Export/Import Types
    public enum DataFormat
    {
        BasicCsv,
        BasicExcel,
        MatecatCsv,
        MatecatExcel,
        SmartcatCsv,
        SmartcatExcel
    }
}
