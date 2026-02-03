using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Services;

/// <summary>
/// Service for processing and importing images.
/// </summary>
public interface IImageImportService
{
    /// <summary>
    /// Processes an image for import (resize if needed, auto-orient, save).
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="targetPath">Target file path to save the processed image.</param>
    /// <param name="maxDimension">Maximum width or height dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing processed image information.</returns>
    Task<ImageImportResult> ProcessImageAsync(
        Stream sourceStream,
        string targetPath,
        int maxDimension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts metadata from an image (EXIF data, dimensions).
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted metadata or null if extraction fails.</returns>
    Task<ImageMetadata?> ExtractMetadataAsync(
        Stream sourceStream,
        CancellationToken cancellationToken = default);
}
