# LibraFoto API — Module Dependencies

## Project Dependency Graph

```mermaid
graph TD
    API["LibraFoto.Api<br/><i>Host Application</i>"]
    Auth["LibraFoto.Modules.Auth"]
    Admin["LibraFoto.Modules.Admin"]
    Display["LibraFoto.Modules.Display"]
    Media["LibraFoto.Modules.Media"]
    Storage["LibraFoto.Modules.Storage"]
    Data["LibraFoto.Data"]
    Shared["LibraFoto.Shared"]
    Defaults["LibraFoto.ServiceDefaults"]
    AppHost["LibraFoto.AppHost<br/><i>Dev Orchestrator</i>"]

    API --> Auth
    API --> Admin
    API --> Display
    API --> Media
    API --> Storage
    API --> Defaults

    Admin --> Data
    Admin --> Shared
    Admin --> Media
    Admin --> Storage

    Auth --> Data
    Auth --> Shared

    Display --> Data
    Display --> Shared

    Media --> Data
    Media --> Shared
    Media --> Storage

    Storage --> Data
    Storage --> Shared

    Data --> Shared

    AppHost -.->|"Aspire orchestration"| API

    classDef host fill:#4a90d9,stroke:#2c5282,color:#fff
    classDef module fill:#48bb78,stroke:#276749,color:#fff
    classDef infra fill:#ed8936,stroke:#c05621,color:#fff
    classDef shared fill:#9f7aea,stroke:#6b46c1,color:#fff

    class API,AppHost host
    class Auth,Admin,Display,Media,Storage module
    class Data,Defaults infra
    class Shared shared
```

## Dependency Matrix

| Project ↓ depends on → | Data | Shared | Auth | Admin | Display | Media | Storage | ServiceDefaults |
| ---------------------- | :--: | :----: | :--: | :---: | :-----: | :---: | :-----: | :-------------: |
| **LibraFoto.Api**      |  —   |   —    |  ✓   |   ✓   |    ✓    |   ✓   |    ✓    |        ✓        |
| **Modules.Auth**       |  ✓   |   ✓    |  —   |   —   |    —    |   —   |    —    |        —        |
| **Modules.Admin**      |  ✓   |   ✓    |  —   |   —   |    —    |   ✓   |    ✓    |        —        |
| **Modules.Display**    |  ✓   |   ✓    |  —   |   —   |    —    |   —   |    —    |        —        |
| **Modules.Media**      |  ✓   |   ✓    |  —   |   —   |    —    |   —   |    ✓    |        —        |
| **Modules.Storage**    |  ✓   |   ✓    |  —   |   —   |    —    |   —   |    —    |        —        |
| **LibraFoto.Data**     |  —   |   ✓    |  —   |   —   |    —    |   —   |    —    |        —        |

## Cross-Module Service Dependencies

```mermaid
graph LR
    subgraph "Admin Module"
        PhotoService["PhotoService"]
    end

    subgraph "Media Module"
        IThumbnailService["IThumbnailService"]
    end

    subgraph "Storage Module"
        IStorageProviderFactory["IStorageProviderFactory"]
    end

    PhotoService -->|"Delete thumbnails"| IThumbnailService
    PhotoService -->|"Delete source files"| IStorageProviderFactory

    subgraph "Media Module "
        PhotoEndpoints_Media["PhotoEndpoints<br/>(File Serving)"]
        ThumbnailEndpoints["ThumbnailEndpoints"]
    end

    PhotoEndpoints_Media -->|"Resolve cloud/local files"| IStorageProviderFactory
    ThumbnailEndpoints -->|"Download source for<br/>thumbnail generation"| IStorageProviderFactory
```

## NuGet Package Dependencies

```mermaid
graph TD
    subgraph "LibraFoto.Api"
        JWT_Pkg["Microsoft.AspNetCore<br/>.Authentication.JwtBearer"]
        Serilog_Pkg["Serilog.AspNetCore"]
        SerilogFile["Serilog.Sinks.File"]
        EFDesign_Api["EF Core Design"]
        Docker_Pkg["VS Azure Containers"]
    end

    subgraph "LibraFoto.Data"
        EFCore["Microsoft.EntityFrameworkCore"]
        EFSqlite["EF Core SQLite"]
        EFDesign["EF Core Design"]
        NanoId["Nanoid"]
    end

    subgraph "LibraFoto.Modules.Auth"
        BCrypt["BCrypt.Net-Next"]
        JWT_Auth["JwtBearer"]
    end

    subgraph "LibraFoto.Modules.Media"
        ImageSharp_M["SixLabors.ImageSharp"]
        MetadataExtractor["MetadataExtractor"]
    end

    subgraph "LibraFoto.Modules.Storage"
        ImageSharp_S["SixLabors.ImageSharp"]
        GoogleAuth["Google.Apis.Auth"]
    end

    classDef pkg fill:#e2e8f0,stroke:#a0aec0,color:#2d3748
    class JWT_Pkg,Serilog_Pkg,SerilogFile,EFDesign_Api,Docker_Pkg,EFCore,EFSqlite,EFDesign,NanoId,BCrypt,JWT_Auth,ImageSharp_M,MetadataExtractor,ImageSharp_S,GoogleAuth pkg
```

## Service Registration & Lifetimes

```mermaid
graph TD
    subgraph "Singleton Services"
        direction LR
        SystemService["SystemService"]
        MediaScannerService["MediaScannerService"]
        MemoryCache["IMemoryCache"]
    end

    subgraph "Scoped Services (per-request)"
        direction LR
        PhotoService["PhotoService"]
        AlbumService["AlbumService"]
        TagService["TagService"]
        AuthService["AuthService"]
        UserService["UserService"]
        SetupService["SetupService"]
        GuestLinkService["GuestLinkService"]
        DisplaySettingsService["DisplaySettingsService"]
        SlideshowService["SlideshowService"]
        ThumbnailService["ThumbnailService"]
        MetadataService["MetadataService"]
        ImageProcessor["ImageProcessor"]
        GeocodingService["GeocodingService"]
        StorageProviderFactory["StorageProviderFactory"]
        SyncService["SyncService"]
        ImageImportService["ImageImportService"]
        GooglePickerService["GooglePhotosPickerService"]
    end

    subgraph "Transient (Factory-Created)"
        direction LR
        LocalProvider["LocalStorageProvider"]
        GoogleProvider["GooglePhotosProvider"]
    end

    StorageProviderFactory -->|"Creates"| LocalProvider
    StorageProviderFactory -->|"Creates"| GoogleProvider

    classDef singleton fill:#fc8181,stroke:#c53030,color:#fff
    classDef scoped fill:#63b3ed,stroke:#2b6cb0,color:#fff
    classDef transient fill:#68d391,stroke:#276749,color:#fff

    class SystemService,MediaScannerService,MemoryCache singleton
    class PhotoService,AlbumService,TagService,AuthService,UserService,SetupService,GuestLinkService,DisplaySettingsService,SlideshowService,ThumbnailService,MetadataService,ImageProcessor,GeocodingService,StorageProviderFactory,SyncService,ImageImportService,GooglePickerService scoped
    class LocalProvider,GoogleProvider transient
```
