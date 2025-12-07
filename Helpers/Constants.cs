namespace Zinote.Helpers
{
    public static class Constants
    {
        public const string CredentialsFileName = "firebase-credentials.json";
        public const string AppName = "zi-lex";

        
        // Navigation Parameters
        public const string NavItemId = "ItemId";
        public const string NavCollectionName = "CollectionName";

        // Configuration
        public static bool UseFirebase = false;
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
