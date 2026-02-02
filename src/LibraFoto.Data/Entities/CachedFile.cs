namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a cached file from a cloud storage provider.
/// Used for offline access and performance optimization.
/// </summary>
public class CachedFile
{
    /// <summary>
    /// Unique identifier for the cached file.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// SHA256 hash of the file content (used as cache key).
    /// </summary>
    public required string FileHash { get; set; }

    /// <summary>
    /// Original URL or identifier from the storage provider.
    /// </summary>
    public required string OriginalUrl { get; set; }

    /// <summary>
    /// Storage provider ID that this file came from.
    /// </summary>
    public long ProviderId { get; set; }

    /// <summary>
    /// Provider-specific file identifier (if available).
    /// </summary>
    public string? ProviderFileId { get; set; }

    /// <summary>
    /// Picker session identifier that imported this file (if applicable).
    /// </summary>
    public string? PickerSessionId { get; set; }

    /// <summary>
    /// Local file path where the cached file is stored.
    /// </summary>
    public required string LocalPath { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MIME content type of the file.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// When the file was first cached.
    /// </summary>
    public DateTime CachedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the file was last accessed (for LRU eviction).
    /// </summary>
    public DateTime LastAccessedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times the file has been accessed.
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Navigation property to storage provider.
    /// </summary>
    public StorageProvider? Provider { get; set; }
}
