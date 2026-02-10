using LibraFoto.Modules.Media.Models;

namespace LibraFoto.Modules.Media.Services;

/// <summary>
/// Service for image processing operations like resize, rotate, and format conversion.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Processes an image with the specified options.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="outputStream">Output stream for processed image.</param>
    /// <param name="options">Processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if processing succeeded.</returns>
    Task<bool> ProcessAsync(
        Stream sourceStream,
        Stream outputStream,
        ProcessingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an image file and saves to a new location.
    /// </summary>
    /// <param name="sourcePath">Path to source image.</param>
    /// <param name="outputPath">Path for output image.</param>
    /// <param name="options">Processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if processing succeeded.</returns>
    Task<bool> ProcessAsync(
        string sourcePath,
        string outputPath,
        ProcessingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an image and returns the result as a byte array.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="options">Processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processed image as byte array, or null if processing failed.</returns>
    Task<byte[]?> ProcessToBytesAsync(
        Stream sourceStream,
        ProcessingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes an image to fit within the specified maximum dimensions.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="outputStream">Output stream for resized image.</param>
    /// <param name="maxWidth">Maximum width in pixels.</param>
    /// <param name="maxHeight">Maximum height in pixels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if resize succeeded.</returns>
    Task<bool> ResizeAsync(
        Stream sourceStream,
        Stream outputStream,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates an image by the specified angle.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="outputStream">Output stream for rotated image.</param>
    /// <param name="degrees">Rotation angle in degrees (clockwise). Must be 0, 90, 180, or 270.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rotation succeeded.</returns>
    Task<bool> RotateAsync(
        Stream sourceStream,
        Stream outputStream,
        int degrees,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an image to a different format.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="outputStream">Output stream for converted image.</param>
    /// <param name="outputFormat">Target format.</param>
    /// <param name="quality">Quality level (1-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if conversion succeeded.</returns>
    Task<bool> ConvertAsync(
        Stream sourceStream,
        Stream outputStream,
        ImageOutputFormat outputFormat,
        int quality = 85,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-orients an image based on EXIF orientation data.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="outputStream">Output stream for oriented image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if orientation was applied.</returns>
    Task<bool> AutoOrientAsync(
        Stream sourceStream,
        Stream outputStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the dimensions of an image without fully loading it.
    /// </summary>
    /// <param name="stream">Image stream.</param>
    /// <returns>Tuple of (width, height), or null if unable to determine.</returns>
    (int Width, int Height)? GetDimensions(Stream stream);

    /// <summary>
    /// Checks if the given file extension is a supported image format.
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot).</param>
    /// <returns>True if supported.</returns>
    bool IsSupportedFormat(string extension);

    /// <summary>
    /// Gets the file extension for an output format.
    /// </summary>
    /// <param name="format">Output format.</param>
    /// <returns>File extension without leading dot.</returns>
    string GetExtension(ImageOutputFormat format);

    /// <summary>
    /// Gets the MIME content type for an output format.
    /// </summary>
    /// <param name="format">Output format.</param>
    /// <returns>MIME content type.</returns>
    string GetContentType(ImageOutputFormat format);
}
