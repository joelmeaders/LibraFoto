using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Display.Models;

/// <summary>
/// Photo data transfer object for display frontend.
/// Contains only the information needed for the slideshow.
/// </summary>
public record PhotoDto
{
    /// <summary>
    /// Unique identifier for the photo.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// URL to the full-size photo for display.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// URL to the thumbnail for preloading/preview.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Date the photo was taken (for overlay display).
    /// </summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>
    /// Location where the photo was taken (for overlay display).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Type of media (Photo or Video).
    /// </summary>
    public MediaType MediaType { get; init; }

    /// <summary>
    /// Duration in seconds for video files. Null for photos.
    /// </summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Width of the photo in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Height of the photo in pixels.
    /// </summary>
    public int Height { get; init; }
}
