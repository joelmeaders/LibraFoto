using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Information about a file discovered during directory scanning.
/// </summary>
public record ScannedFile
{
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Relative path from the scan root.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Filename without path.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File extension (lowercase, with dot).
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// MIME content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Type of media (Photo or Video).
    /// </summary>
    public MediaType MediaType { get; init; }

    /// <summary>
    /// File creation time.
    /// </summary>
    public DateTime CreatedTime { get; init; }

    /// <summary>
    /// File last modified time.
    /// </summary>
    public DateTime ModifiedTime { get; init; }

    /// <summary>
    /// Whether this file is hidden.
    /// </summary>
    public bool IsHidden { get; init; }
}
