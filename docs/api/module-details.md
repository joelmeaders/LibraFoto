# LibraFoto API — Module Details

## Auth Module

### Services

| Service             | Implementation     | Lifetime | Purpose                                                                |
| ------------------- | ------------------ | -------- | ---------------------------------------------------------------------- |
| `IAuthService`      | `AuthService`      | Scoped   | JWT authentication: login, logout, token generation/validation/refresh |
| `IUserService`      | `UserService`      | Scoped   | User CRUD, password hashing (BCrypt), email uniqueness                 |
| `ISetupService`     | `SetupService`     | Scoped   | First-run setup: creates initial Admin user                            |
| `IGuestLinkService` | `GuestLinkService` | Scoped   | Guest upload link CRUD, validation, upload tracking                    |

### Auth Flow

```mermaid
sequenceDiagram
    participant Client
    participant AuthEndpoint
    participant AuthService
    participant UserService
    participant DB

    Client->>AuthEndpoint: POST /api/auth/login {email, password}
    AuthEndpoint->>AuthService: LoginAsync(email, password)
    AuthService->>UserService: GetByEmailAsync(email)
    UserService->>DB: Query Users
    DB-->>UserService: User entity
    UserService-->>AuthService: User
    AuthService->>AuthService: BCrypt.Verify(password, hash)
    AuthService->>AuthService: GenerateJWT(claims)
    AuthService->>AuthService: GenerateRefreshToken()
    AuthService-->>AuthEndpoint: LoginResponse
    AuthEndpoint-->>Client: {token, refreshToken, expiresAt, user}
```

### JWT Claims

| Claim            | Source           | Description                       |
| ---------------- | ---------------- | --------------------------------- |
| `NameIdentifier` | `User.Id`        | User's database ID                |
| `Name`           | `User.Email`     | User's email address              |
| `Email`          | `User.Email`     | Duplicate for compatibility       |
| `Role`           | `User.Role`      | Role name (Admin/Editor/Guest)    |
| `role_level`     | `(int)User.Role` | Integer role level for comparison |

---

## Admin Module

### Services

| Service          | Implementation  | Lifetime  | Dependencies                                          | Purpose                                          |
| ---------------- | --------------- | --------- | ----------------------------------------------------- | ------------------------------------------------ |
| `IPhotoService`  | `PhotoService`  | Scoped    | DbContext, IThumbnailService, IStorageProviderFactory | Photo CRUD, bulk operations, file cleanup        |
| `IAlbumService`  | `AlbumService`  | Scoped    | DbContext                                             | Album CRUD, photo ordering, cover photos         |
| `ITagService`    | `TagService`    | Scoped    | DbContext                                             | Tag CRUD, photo-tag associations                 |
| `ISystemService` | `SystemService` | Singleton | IMemoryCache, IHostEnvironment                        | System info, update checks (git), update trigger |

### Photo Delete Flow

```mermaid
flowchart TD
    Delete["DeletePhotoAsync(id)"] --> BeginTx["Begin DB Transaction"]
    BeginTx --> FindPhoto["Find Photo in DB"]
    FindPhoto --> RemoveDB["Remove from DB<br/>(cascades junctions)"]
    RemoveDB --> HasProvider{"Has Storage<br/>Provider?"}
    HasProvider -->|Yes| ProviderDelete["StorageProvider.DeleteFileAsync()"]
    HasProvider -->|No| LocalDelete["Delete local file<br/>(FilePath)"]
    ProviderDelete --> DeleteThumb["ThumbnailService.DeleteThumbnails()"]
    LocalDelete --> DeleteThumb
    DeleteThumb --> DeleteThumbPath{"ThumbnailPath<br/>exists?"}
    DeleteThumbPath -->|Yes| DeleteFile["Delete thumbnail file"]
    DeleteThumbPath -->|No| Commit
    DeleteFile --> Commit["Commit Transaction"]
    Commit --> Success["Return NoContent"]

    FindPhoto -->|Not Found| NotFound["Return 404"]
    ProviderDelete -->|Error| Rollback["Rollback Transaction"]
    LocalDelete -->|Error| Rollback
```

---

## Display Module

### Services

| Service                   | Implementation           | Lifetime | Purpose                                                             |
| ------------------------- | ------------------------ | -------- | ------------------------------------------------------------------- |
| `IDisplaySettingsService` | `DisplaySettingsService` | Scoped   | Display settings CRUD, auto-default creation, activation management |
| `ISlideshowService`       | `SlideshowService`       | Scoped   | Photo queue management, sequence control, preloading                |

### Slideshow State Machine

```mermaid
stateDiagram-v2
    [*] --> Empty: Initial

    Empty --> BuildQueue: GetNextPhoto()
    BuildQueue --> LoadSettings: Read DisplaySettings

    state LoadSettings {
        [*] --> CheckSource
        CheckSource --> FilterAll: SourceType=All
        CheckSource --> FilterAlbum: SourceType=Album
        CheckSource --> FilterTag: SourceType=Tag
        FilterAll --> QueryPhotos
        FilterAlbum --> QueryPhotos
        FilterTag --> QueryPhotos
        QueryPhotos --> Shuffle: shuffle=true
        QueryPhotos --> Sequential: shuffle=false
        Shuffle --> QueueReady
        Sequential --> QueueReady
    }

    LoadSettings --> Ready: Queue built

    Ready --> Dequeue: GetNextPhoto()
    Dequeue --> Ready: Queue has items
    Dequeue --> BuildQueue: Queue empty (wrap around)

    Ready --> Empty: ResetSequence()
    Ready --> Ready: GetCurrentPhoto() (no advance)
    Ready --> Ready: GetPreloadPhotos() (peek only)

    note right of Ready
        State stored in static
        ConcurrentDictionary keyed
        by settingsId
    end note
```

### Display Config Resolution

```mermaid
flowchart TD
    Request["GET /api/display/config"] --> CheckForwarded{"X-Forwarded-Host<br/>header present?"}
    CheckForwarded -->|"Yes (non-localhost)"| UseForwarded["Use forwarded host"]
    CheckForwarded -->|No| CheckEnvVar{"LIBRAFOTO_HOST_IP<br/>env var set?"}
    CheckEnvVar -->|Yes| UseEnvIP["Use configured IP"]
    CheckEnvVar -->|No| DetectLAN["Auto-detect LAN IP<br/>(filter virtual/Docker NICs)"]
    DetectLAN --> HasLAN{"Found LAN IP?"}
    HasLAN -->|Yes| UseLAN["Use detected LAN IP"]
    HasLAN -->|No| UseHost["Use request Host header"]
    UseForwarded --> BuildURL["Build Admin URL"]
    UseEnvIP --> BuildURL
    UseLAN --> BuildURL
    UseHost --> BuildURL
    BuildURL --> Response["{ adminUrl: 'http://host:port' }"]
```

---

## Media Module

### Services

| Service             | Implementation     | Lifetime | Dependencies               | Purpose                                                         |
| ------------------- | ------------------ | -------- | -------------------------- | --------------------------------------------------------------- |
| `IThumbnailService` | `ThumbnailService` | Scoped   | ImageSharp, IConfiguration | 400×400 JPEG thumbnails, year/month directory structure         |
| `IMetadataService`  | `MetadataService`  | Scoped   | MetadataExtractor          | EXIF/GPS extraction from images                                 |
| `IImageProcessor`   | `ImageProcessor`   | Scoped   | ImageSharp                 | General image processing (resize, rotate, convert, auto-orient) |
| `IGeocodingService` | `GeocodingService` | Scoped   | HttpClient (Nominatim)     | Reverse geocoding with rate limiting (1 req/sec, 60 req/min)    |

### Thumbnail Resolution

```mermaid
flowchart TD
    Request["GET /api/media/thumbnails/{photoId}"] --> FindPhoto["Find Photo in DB"]
    FindPhoto -->|Not Found| Return404["404 Not Found"]
    FindPhoto -->|Found| CheckManaged{"Managed thumbnail<br/>exists?"}
    CheckManaged -->|Yes| ServeManaged["Serve from<br/>.thumbnails/YYYY/MM/{id}.jpg"]
    CheckManaged -->|No| CheckPath{"photo.ThumbnailPath<br/>set?"}
    CheckPath -->|Yes| CheckPathExists{"File exists<br/>on disk?"}
    CheckPathExists -->|Yes| ServePath["Serve from ThumbnailPath"]
    CheckPathExists -->|No| AutoGenerate
    CheckPath -->|No| AutoGenerate["Auto-generate from source"]
    AutoGenerate --> HasProvider{"Has Storage<br/>Provider?"}
    HasProvider -->|Yes| DownloadSource["Download from provider"]
    HasProvider -->|No| ReadLocal["Read local file"]
    DownloadSource --> Generate["Generate 400×400 JPEG<br/>(Lanczos3, quality 85)"]
    ReadLocal --> Generate
    Generate --> UpdateDB["Update ThumbnailPath in DB"]
    UpdateDB --> ServeGenerated["Serve generated thumbnail"]
```

---

## Storage Module

### Services

| Service                     | Implementation           | Lifetime  | Purpose                                   |
| --------------------------- | ------------------------ | --------- | ----------------------------------------- |
| `IStorageProviderFactory`   | `StorageProviderFactory` | Scoped    | Creates/caches storage provider instances |
| `IMediaScannerService`      | `MediaScannerService`    | Singleton | Directory scanning, file type detection   |
| `ISyncService`              | `SyncService`            | Scoped    | Provider ↔ DB synchronization engine      |
| `IImageImportService`       | `ImageImportService`     | Scoped    | Image processing during import            |
| `GooglePhotosPickerService` | (concrete)               | Scoped    | Google Photos Picker API client           |

### Supported File Types

| Category   | Extensions                                                  |
| ---------- | ----------------------------------------------------------- |
| **Images** | jpg, jpeg, png, gif, webp, bmp, tiff, tif, heic, heif, avif |
| **Videos** | mp4, mov, avi, mkv, webm, m4v, 3gp, wmv, flv                |
