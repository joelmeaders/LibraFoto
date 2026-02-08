using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Models
{
    /// <summary>
    /// DTO for storage provider information sent to the frontend.
    /// </summary>
    public record StorageProviderDto
    {
        /// <summary>
        /// Database ID of the storage provider.
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// Type of storage provider.
        /// </summary>
        public StorageProviderType Type { get; init; }

        /// <summary>
        /// Display name for this provider instance.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this provider is currently enabled.
        /// </summary>
        public bool IsEnabled { get; init; }

        /// <summary>
        /// Whether this provider supports file uploads.
        /// </summary>
        public bool SupportsUpload { get; init; }

        /// <summary>
        /// Whether this provider supports folder watching.
        /// </summary>
        public bool SupportsWatch { get; init; }

        /// <summary>
        /// Date of the last successful sync.
        /// </summary>
        public DateTime? LastSyncDate { get; init; }

        /// <summary>
        /// Number of photos from this provider.
        /// </summary>
        public int PhotoCount { get; init; }

        /// <summary>
        /// Whether the provider connection is healthy.
        /// </summary>
        public bool? IsConnected { get; init; }

        /// <summary>
        /// Human-readable status message.
        /// </summary>
        public string? StatusMessage { get; init; }
    }

    /// <summary>
    /// Request to create a new storage provider.
    /// </summary>
    public record CreateStorageProviderRequest
    {
        /// <summary>
        /// Type of storage provider to create.
        /// </summary>
        public StorageProviderType Type { get; init; }

        /// <summary>
        /// Display name for this provider instance.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Configuration specific to the provider type.
        /// For local storage: { "basePath": "/path/to/photos" }
        /// For Google Photos: OAuth configuration
        /// </summary>
        public string? Configuration { get; init; }

        /// <summary>
        /// Whether to enable this provider immediately.
        /// </summary>
        public bool IsEnabled { get; init; } = true;
    }

    /// <summary>
    /// Request to update an existing storage provider.
    /// </summary>
    public record UpdateStorageProviderRequest
    {
        /// <summary>
        /// New display name (optional).
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Updated configuration (optional).
        /// </summary>
        public string? Configuration { get; init; }

        /// <summary>
        /// Whether to enable/disable the provider (optional).
        /// </summary>
        public bool? IsEnabled { get; init; }
    }

    /// <summary>
    /// Configuration for local storage provider.
    /// </summary>
    public record LocalStorageConfiguration
    {
        /// <summary>
        /// Base path for storing photos.
        /// Defaults to "./photos" or "/photos" in container.
        /// </summary>
        public string BasePath { get; init; } = "./photos";

        /// <summary>
        /// Whether to organize uploads by year/month folders.
        /// </summary>
        public bool OrganizeByDate { get; init; } = true;

        /// <summary>
        /// Whether to watch the folder for new files.
        /// </summary>
        public bool WatchForChanges { get; init; } = true;

        /// <summary>
        /// Maximum dimension (width or height) for imported images.
        /// Images larger than this will be resized while preserving aspect ratio.
        /// Default is 2560 pixels.
        /// </summary>
        public int MaxImportDimension { get; init; } = 2560;
    }
}
