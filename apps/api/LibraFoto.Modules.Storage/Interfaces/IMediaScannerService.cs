using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Interfaces;

/// <summary>
/// Service for scanning directories and identifying media files.
/// </summary>
public interface IMediaScannerService
{
    /// <summary>
    /// Supported image file extensions (lowercase, with dot).
    /// </summary>
    IReadOnlySet<string> SupportedImageExtensions { get; }

    /// <summary>
    /// Supported video file extensions (lowercase, with dot).
    /// </summary>
    IReadOnlySet<string> SupportedVideoExtensions { get; }

    /// <summary>
    /// Scans a directory for media files.
    /// </summary>
    /// <param name="directoryPath">The path to the directory to scan.</param>
    /// <param name="recursive">Whether to scan subdirectories.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered media files.</returns>
    Task<IEnumerable<ScannedFile>> ScanDirectoryAsync(string directoryPath, bool recursive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file is a supported media file based on its extension.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file is a supported media type.</returns>
    bool IsSupportedMediaFile(string filePath);

    /// <summary>
    /// Checks if a file is a supported image based on its extension.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file is a supported image type.</returns>
    bool IsSupportedImage(string filePath);

    /// <summary>
    /// Checks if a file is a supported video based on its extension.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file is a supported video type.</returns>
    bool IsSupportedVideo(string filePath);

    /// <summary>
    /// Gets the MIME content type for a file based on its extension.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The MIME content type, or "application/octet-stream" if unknown.</returns>
    string GetContentType(string filePath);

    /// <summary>
    /// Generates a unique filename to avoid collisions.
    /// </summary>
    /// <param name="originalFilename">The original filename.</param>
    /// <param name="targetDirectory">The target directory to check for collisions.</param>
    /// <returns>A unique filename.</returns>
    string GenerateUniqueFilename(string originalFilename, string targetDirectory);
}
