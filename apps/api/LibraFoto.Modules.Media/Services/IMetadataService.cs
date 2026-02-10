using LibraFoto.Modules.Media.Models;

namespace LibraFoto.Modules.Media.Services;

/// <summary>
/// Service for extracting metadata from image and video files.
/// </summary>
public interface IMetadataService
{
    /// <summary>
    /// Extracts metadata from an image stream.
    /// </summary>
    /// <param name="stream">Source stream containing the image data.</param>
    /// <param name="fileName">Optional filename for format detection.</param>
    /// <returns>Extracted metadata.</returns>
    ImageMetadata ExtractMetadata(Stream stream, string? fileName = null);

    /// <summary>
    /// Extracts metadata from a file path.
    /// </summary>
    /// <param name="filePath">Path to the image or video file.</param>
    /// <returns>Extracted metadata.</returns>
    ImageMetadata ExtractMetadata(string filePath);

    /// <summary>
    /// Extracts metadata asynchronously from a stream.
    /// </summary>
    /// <param name="stream">Source stream containing the image data.</param>
    /// <param name="fileName">Optional filename for format detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted metadata.</returns>
    Task<ImageMetadata> ExtractMetadataAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts metadata asynchronously from a file path.
    /// </summary>
    /// <param name="filePath">Path to the image or video file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted metadata.</returns>
    Task<ImageMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts only date information from an image.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>Date taken if found, otherwise null.</returns>
    DateTime? ExtractDateTaken(Stream stream);

    /// <summary>
    /// Extracts only GPS coordinates from an image.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>Tuple of (latitude, longitude) if found, otherwise null.</returns>
    (double Latitude, double Longitude)? ExtractGpsCoordinates(Stream stream);

    /// <summary>
    /// Extracts image dimensions without loading the full image.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>Tuple of (width, height) if found, otherwise null.</returns>
    (int Width, int Height)? ExtractDimensions(Stream stream);
}
