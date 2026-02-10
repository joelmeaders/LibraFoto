using LibraFoto.Modules.Display.Models;

namespace LibraFoto.Modules.Display.Services;

/// <summary>
/// Interface for slideshow operations.
/// Manages photo queue, rotation, and preloading for the display.
/// </summary>
public interface ISlideshowService
{
    /// <summary>
    /// Gets the next photo in the slideshow sequence.
    /// Advances the internal pointer and returns the next photo based on current settings.
    /// </summary>
    /// <param name="settingsId">Optional settings ID. Uses active settings if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next photo to display, or null if no photos are available.</returns>
    Task<PhotoDto?> GetNextPhotoAsync(long? settingsId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current photo being displayed.
    /// Does not advance the sequence.
    /// </summary>
    /// <param name="settingsId">Optional settings ID. Uses active settings if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current photo, or null if no photos are available.</returns>
    Task<PhotoDto?> GetCurrentPhotoAsync(long? settingsId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple photos for frontend preloading/caching.
    /// </summary>
    /// <param name="count">Number of photos to preload (default: 10).</param>
    /// <param name="settingsId">Optional settings ID. Uses active settings if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of photos for preloading.</returns>
    Task<IReadOnlyList<PhotoDto>> GetPreloadPhotosAsync(int count = 10, long? settingsId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the slideshow sequence.
    /// Called when settings change or user manually resets.
    /// </summary>
    /// <param name="settingsId">Optional settings ID. Resets for active settings if not specified.</param>
    void ResetSequence(long? settingsId = null);

    /// <summary>
    /// Gets the total number of photos available for the current settings.
    /// </summary>
    /// <param name="settingsId">Optional settings ID. Uses active settings if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total photo count.</returns>
    Task<int> GetPhotoCountAsync(long? settingsId = null, CancellationToken cancellationToken = default);
}
