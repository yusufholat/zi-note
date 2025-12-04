# Zinote

Zinote is a cross-platform dictionary management application built with .NET MAUI. It provides a comprehensive solution for managing multiple specialized dictionary collections with support for bilingual terms, definitions, and seamless data export.

## Features

### Multi-Collection Management
- **Hub-based Navigation**: Access different dictionary collections from a central hub
- **Specialized Collections**: Pre-configured collections for different domains (Health Dictionary, Military Dictionary, etc.)
- **Dynamic Collection Support**: Easily switch between different dictionary collections

### Dictionary Item Management
- **Bilingual Support**: Manage source terms (English) and target terms (Turkish)
- **Rich Definitions**: Add detailed definitions for each term
- **CRUD Operations**: Create, read, update, and delete dictionary entries
- **Real-time Search**: Search across source terms, target terms, and definitions

### Data Export
- **CSV Export**: Export dictionary data to standard CSV format
- **Matecat Export**: Export data in Matecat-compatible format for translation workflows
- **Organized Exports**: Files are saved with timestamps in the Documents folder

### Cloud Storage
- **Firebase Firestore Integration**: Cloud-based data storage and synchronization
- **Secure Authentication**: Firebase credentials management
- **Real-time Updates**: Automatic data synchronization

### Cross-Platform Support
- **Windows**: Native Windows application
- **Android**: Mobile app support
- **iOS**: iPhone and iPad support
- **macOS**: Mac Catalyst support

## Technology Stack

- **.NET MAUI**: Cross-platform framework
- **Firebase Firestore**: Cloud database
- **C#**: Primary programming language
- **XAML**: UI markup language

## Getting Started

### Prerequisites

- .NET 7.0 SDK or later
- Visual Studio 2022 (with .NET MAUI workload) or Visual Studio Code
- Firebase project with Firestore enabled

### Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd zi-note
   ```

2. **Configure Firebase**
   - Create a Firebase project at [Firebase Console](https://console.firebase.google.com/)
   - Enable Firestore Database
   - Download your service account credentials JSON file
   - Place the file as `firebase-credentials.json` in the project root

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the project**
   ```bash
   dotnet build
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

## Project Structure

```
zi-note/
├── Models/
│   └── DictionaryItem.cs      # Data model for dictionary entries
├── Pages/
│   ├── HubPage.xaml            # Main hub for collection selection
│   ├── HubPage.xaml.cs
│   ├── ItemDetailPage.xaml     # Item creation/editing page
│   └── ItemDetailPage.xaml.cs
├── Services/
│   ├── DataService.cs          # Main data service with Firestore integration
│   └── FirestoreService.cs     # Firebase Firestore service
├── Platforms/                  # Platform-specific code
│   ├── Android/
│   ├── iOS/
│   ├── Windows/
│   └── MacCatalyst/
├── Resources/                   # App resources (fonts, images, styles)
└── Zinote.csproj               # Project file
```

## Usage

1. **Launch the Application**: Start the app to see the Hub page with available dictionary collections
2. **Select a Collection**: Tap on a collection card (e.g., Health Dictionary, Military Dictionary)
3. **Manage Items**: 
   - Use the search bar to find specific terms
   - Tap "Add New" to create a new dictionary entry
   - Tap on an existing item to edit it
   - Use the delete button to remove items
4. **Export Data**: 
   - Click "Export CSV" for standard CSV format
   - Click "Export Matecat" for Matecat translation format

## Configuration

### Firebase Project ID
The Firebase project ID is configured in `Services/DataService.cs`. Update the `ProjectId` constant if needed:
```csharp
private const string ProjectId = "zinote-83c37";
```

### Default Collection
The default collection name is `"dictionary_items"` but can be changed per collection in the Hub page.

## Security Notes

- `firebase-credentials.json` is excluded from version control for security
- Never commit Firebase credentials to the repository
- Use environment variables or secure credential storage in production

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

[Add your license here]

## Support

For issues and questions, please open an issue in the repository.
