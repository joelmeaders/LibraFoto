using LibraFoto.Modules.Media.Models;

namespace LibraFoto.Modules.Media.Services;

/// <summary>
/// Service for generating thumbnails from images and videos.
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Generates a 400x400 thumbnail for the given image file.
    /// </summary>
    /// <param name="sourceStream">Source image stream.</param>
    /// <param name="photoId">Photo ID for naming the thumbnail file.</param>
    /// <param name="dateTaken">Date taken for organizing thumbnail paths by year/month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing thumbnail path and dimensions.</returns>
    Task<ThumbnailResult> GenerateThumbnailAsync(
        Stream sourceStream,
        long photoId,
        DateTime dateTaken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a 400x400 thumbnail from a file path.
    /// </summary>
    /// <param name="sourcePath">Path to the source image file.</param>
    /// <param name="photoId">Photo ID for naming the thumbnail file.</param>
    /// <param name="dateTaken">Date taken for organizing thumbnail paths by year/month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing thumbnail path and dimensions.</returns>
    Task<ThumbnailResult> GenerateThumbnailAsync(
        string sourcePath,
        long photoId,
        DateTime dateTaken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to an existing thumbnail.
    /// </summary>
    /// <param name="photoId">Photo ID.</param>
    /// <returns>Path to thumbnail file, or null if it doesn't exist.</returns>
    string? GetThumbnailPath(long photoId);

    /// <summary>
    /// Gets the absolute path to an existing thumbnail.
    /// </summary>
    /// <param name="photoId">Photo ID.</param>
    /// <returns>Absolute path to thumbnail file, or null if it doesn't exist.</returns>
    string? GetThumbnailAbsolutePath(long photoId);

    /// <summary>
    /// Opens a stream to read an existing thumbnail.
    /// </summary>
    /// <param name="photoId">Photo ID.</param>
    /// <returns>Stream to the thumbnail, or null if it doesn't exist.</returns>
    Stream? OpenThumbnailStream(long photoId);

    /// <summary>
    /// Checks if a thumbnail exists.
    /// </summary>
    /// <param name="photoId">Photo ID.</param>
    /// <returns>True if the thumbnail exists.</returns>
    bool ThumbnailExists(long photoId);

    /// <summary>
    /// Deletes the thumbnail for a photo.
    /// </summary>
    /// <param name="photoId">Photo ID.</param>
    /// <returns>True if thumbnail was deleted.</returns>
    bool DeleteThumbnails(long photoId);

    /// <summary>
    /// Gets the base path for thumbnail storage.
    /// </summary>
    string ThumbnailBasePath { get; }
}
