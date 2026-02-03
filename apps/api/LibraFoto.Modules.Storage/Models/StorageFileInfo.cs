using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Information about a file in a storage provider.
/// </summary>
public record StorageFileInfo
{
    /// <summary>
    /// Unique identifier for this file within the storage provider.
    /// For local storage, this is the relative path from the storage root.
    /// For cloud providers, this is the provider's file ID.
    /// </summary>
    public required string FileId { get; init; }

    /// <summary>
    /// The filename (without path).
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The full path or URL to the file.
    /// </summary>
    public string? FullPath { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// MIME content type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Type of media (Photo or Video).
    /// </summary>
    public MediaType MediaType { get; init; }

    /// <summary>
    /// Date the file was created/taken.
    /// </summary>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Date the file was last modified.
    /// </summary>
    public DateTime? ModifiedDate { get; init; }

    /// <summary>
    /// Hash of the file content (for duplicate detection).
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Image/video width in pixels (if available).
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Image/video height in pixels (if available).
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Duration in seconds for video files.
    /// </summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Whether this is a folder/directory.
    /// </summary>
    public bool IsFolder { get; init; }

    /// <summary>
    /// Parent folder ID (for hierarchical storage).
    /// </summary>
    public string? ParentFolderId { get; init; }
}
