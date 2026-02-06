# LibraFoto API — Data Model

## Entity Relationship Diagram

```mermaid
erDiagram TB
    Photo {
        long Id PK
        string Filename
        string OriginalFilename
        string FilePath
        string ThumbnailPath
        int Width
        int Height
        long FileSize
        MediaType MediaType
        double Duration
        DateTime DateTaken
        DateTime DateAdded
        string Location
        double Latitude
        double Longitude
        long ProviderId FK
        string ProviderFileId
    }

    Album {
        long Id PK
        string Name
        string Description
        long CoverPhotoId FK
        DateTime DateCreated
        int SortOrder
    }

    Tag {
        long Id PK
        string Name UK
        string Color
    }

    PhotoAlbum {
        long PhotoId PK_FK
        long AlbumId PK_FK
        int SortOrder
        DateTime DateAdded
    }

    PhotoTag {
        long PhotoId PK_FK
        long TagId PK_FK
        DateTime DateAdded
    }

    User {
        long Id PK
        string Email UK
        string PasswordHash
        UserRole Role
        DateTime DateCreated
        DateTime LastLogin
    }

    StorageProvider {
        long Id PK
        StorageProviderType Type
        string Name
        bool IsEnabled
        string Configuration
        DateTime LastSyncDate
    }

    DisplaySettings {
        long Id PK
        string Name
        int SlideDuration
        TransitionType Transition
        int TransitionDuration
        SourceType SourceType
        long SourceId
        bool Shuffle
        ImageFit ImageFit
        bool IsActive
    }

    GuestLink {
        string Id PK
        string Name
        DateTime ExpiresAt
        int MaxUploads
        int CurrentUploads
        long CreatedById FK
        DateTime DateCreated
        long TargetAlbumId FK
    }

    PickerSession {
        long Id PK
        long ProviderId FK
        string SessionId UK
        string PickerUri
        bool MediaItemsSet
        DateTime CreatedAt
        DateTime ExpiresAt
    }

    Photo ||--o{ PhotoAlbum : "belongs to"
    Album ||--o{ PhotoAlbum : "contains"
    Photo ||--o{ PhotoTag : "tagged with"
    Tag ||--o{ PhotoTag : "applied to"
    StorageProvider ||--o{ Photo : "provides"
    StorageProvider ||--o{ PickerSession : "owns"
    Photo ||--o{ Album : "cover for"
    User ||--o{ GuestLink : "created"
    Album ||--o{ GuestLink : "target for"
```

## Enumerations

```mermaid
classDiagram
    class MediaType {
        Photo = 0
        Video = 1
    }

    class StorageProviderType {
        Local = 0
        GooglePhotos = 1
        GoogleDrive = 2
        OneDrive = 3
    }

    class UserRole {
        Guest = 0
        Editor = 1
        Admin = 2
    }

    class TransitionType {
        Fade = 0
        Slide = 1
        KenBurns = 2
    }

    class SourceType {
        All = 0
        Album = 1
        Tag = 2
    }

    class ImageFit {
        Contain = 0
        Cover = 1
    }
```

## Delete Behavior Map

| Relationship                    | On Delete   | Effect                                  |
| ------------------------------- | ----------- | --------------------------------------- |
| StorageProvider → Photo         | **SetNull** | Photos remain; `ProviderId` set to null |
| StorageProvider → PickerSession | **Cascade** | Picker sessions deleted                 |
| Photo → PhotoAlbum              | **Cascade** | Junction rows removed                   |
| Album → PhotoAlbum              | **Cascade** | Junction rows removed                   |
| Photo → PhotoTag                | **Cascade** | Junction rows removed                   |
| Tag → PhotoTag                  | **Cascade** | Junction rows removed                   |
| Photo → Album (cover)           | **SetNull** | Album's `CoverPhotoId` set to null      |
| User → GuestLink                | **Cascade** | Guest links deleted                     |
| Album → GuestLink (target)      | **SetNull** | GuestLink's `TargetAlbumId` set to null |
