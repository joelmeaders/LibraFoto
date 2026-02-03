using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LibraFoto.Data.Enums;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a photo or video in the library.
/// </summary>
public class Photo
{
    /// <summary>
    /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Current filename (may be renamed from original).
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// Original filename when imported.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the full-size file from storage root.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the thumbnail (400x400px).
    /// </summary>
    [MaxLength(1024)]
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Image/video width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Image/video height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Type of media (Photo or Video).
    /// </summary>
    public MediaType MediaType { get; set; } = MediaType.Photo;

    /// <summary>
    /// Duration in seconds for video files. Null for photos.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Date the photo/video was taken (from EXIF or file metadata).
    /// Stored as ISO 8601 string for SQLite compatibility.
    /// </summary>
    public DateTime? DateTaken { get; set; }

    /// <summary>
    /// Date the photo was added to LibraFoto.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reverse-geocoded location name (e.g., "Paris, France").
    /// </summary>
    [MaxLength(512)]
    public string? Location { get; set; }

    /// <summary>
    /// GPS latitude coordinate.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// GPS longitude coordinate.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Foreign key to the storage provider this photo came from.
    /// </summary>
    public long? ProviderId { get; set; }

    /// <summary>
    /// The file ID from the external storage provider (for sync tracking).
    /// </summary>
    [MaxLength(512)]
    public string? ProviderFileId { get; set; }

    // Navigation properties

    /// <summary>
    /// The storage provider this photo originated from.
    /// </summary>
    [ForeignKey(nameof(ProviderId))]
    public StorageProvider? Provider { get; set; }

    /// <summary>
    /// Albums this photo belongs to.
    /// </summary>
    public ICollection<PhotoAlbum> PhotoAlbums { get; set; } = [];

    /// <summary>
    /// Tags assigned to this photo.
    /// </summary>
    public ICollection<PhotoTag> PhotoTags { get; set; } = [];

    /// <summary>
    /// Albums where this photo is the cover.
    /// </summary>
    public ICollection<Album> CoverForAlbums { get; set; } = [];
}
