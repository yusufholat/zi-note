# zi-lex

**zi-lex** is a specialized terminology management application built with .NET MAUI. It is designed to act as a central hub for managing diverse dictionary collections (Health, Military, etc.), offering advanced features for term definitions, bilingual support (English-Turkish), and professional data export options.

## Features

### 📚 Multi-Collection Hub
- **Centralized Dashboard**: Access all your terminologies from a single, modern "Hub" interface.
- **Specialized Modules**: 
    - **Health Dictionary**: For medical terms and definitions.
    - **Military Dictionary**: For army and defense terminology.
- **Extensible Architecture**: Easily adaptable for new domains (Legal, Technical, etc.).

### 📝 Terminology Management
- **Bilingual Support**: Dedicated fields for Source Term (English) and Target Term (Turkish).
- **Rich Data Model**:
    - Definitions
    - Notes / Comments
    - Examples of Use
    - Domain & Sub-domain tagging
- **Forbidden Terms**: Mark terms as "Do not translate" or "Forbidden".
- **Real-time Search**: Instant filtering by term, translation, or definition.

### 📤 Professional Export
- **Standard Exports**:
    - **CSV**: Basic comma-separated values.
    - **Excel**: Formatted native Excel (.xlsx) files.
- **CAT Tool Integration**:
    - **Matecat**: Specialized CSV and Excel formats ready for Matecat import.
    - **Smartcat**: Customized export formats for Smartcat workflows.

### 📥 Professional Import
- **Bulk Data Import**: Easily import large datasets from CSV or Excel files.
- **Intelligent Parsing**: Automatically detects and parses Matecat and Smartcat formatted files.
- **Batch Processing**: Efficiently uploads validated data to Firebase in batches.

### ☁️ Cloud & Security
- **Firebase Firestore**: Real-time cloud database for instant synchronization across devices.
- **Secure Configuration**: Credential-based access management.

## 🔐 Authentication
The application uses **Firebase Authentication** (Email/Password) to secure access.
- Users must login before accessing the Hub.
- User session is persisted securely.
- "CreatedBy", "ModifiedBy", and "DeletedBy" fields are tracked for audit purposes.
- **Guest Access**: Option to "Continue as Guest" for read-only access without account creation.

## 🎨 Modern UI & UX
- **Redesigned Login Page**: Features a modern dark gradient background, glassmorphism card, and smooth animations.
- **Unified Header**: Consistent navigation bar with left-aligned branding and global profile access across all pages.

## 🗑️ Soft Delete
To prevent data loss, delete operations now perform a **Soft Delete**:
- Items are marked as `IsDeleted = true`.
- They are filtered out from the main list but remain in the database.
- Audit fields `DeletedAt` and `DeletedBy` are populated.

## Technology Stack

- **.NET MAUI 9.0**: Cross-platform framework (Windows, Android, iOS, macOS).
- **C# 12**: Core logic and business rules.
- **Firebase Firestore**: NoSQL cloud database.
- **ClosedXML**: Library for high-fidelity Excel file generation.
- **XAML**: Modern UI definition.

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 (with .NET MAUI workload) or VS Code
- A valid `firebase-credentials.json` file (Google Service Account)

### Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd zi-note
   ```

2. **Configure Credentials**
   - Place your `firebase-credentials.json` file in the root directory.
   - *Note: This file is git-ignored for security.*

3. **Build & Run**
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

## Project Structure

```
zi-note/
├── Helpers/
│   └── Constants.cs            # App-wide constants (Names, Keys)
├── Models/
│   └── DictionaryItem.cs       # Firestore data model
├── Pages/
│   ├── HubPage.xaml            # Main dashboard
│   ├── DictionaryListPage.xaml # List/Search view for a collection
│   └── ItemDetailPage.xaml     # CRUD form
├── Resources/
│   ├── Components/
│   │   └── HeaderView.xaml     # Reusable UI Header
│   └── Languages/              # Localization (.resx)
├── Services/
│   ├── DataService.cs          # Core logic (Firestore)
│   ├── ExportService.cs        # Data export logic (CSV, Excel)
│   └── ImportService.cs        # Data import logic (CSV, Excel)
└── Zinote.csproj               # Project configuration
```

## Contributing

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

Distributed under the MIT License. See `LICENSE` for more information.
