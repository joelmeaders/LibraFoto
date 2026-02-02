using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a guest upload link for sharing with others.
/// Allows guests to upload photos without having a full account.
/// </summary>
public class GuestLink
{
    /// <summary>
    /// Primary key. URL-friendly NanoId used in the link.
    /// </summary>
    [Key]
    [MaxLength(12)]
    public string Id { get; set; } = NanoidDotNet.Nanoid.Generate(size: 12);

    /// <summary>
    /// Friendly name for the link (e.g., "Wedding Photos Upload").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional expiration date for the link.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Maximum number of uploads allowed. Null means unlimited.
    /// </summary>
    public int? MaxUploads { get; set; }

    /// <summary>
    /// Current number of uploads made via this link.
    /// </summary>
    public int CurrentUploads { get; set; } = 0;

    /// <summary>
    /// Foreign key to the user who created this link.
    /// </summary>
    public long CreatedById { get; set; }

    /// <summary>
    /// Date the link was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional album to automatically add uploaded photos to.
    /// </summary>
    public long? TargetAlbumId { get; set; }

    // Navigation properties

    /// <summary>
    /// The user who created this link.
    /// </summary>
    [ForeignKey(nameof(CreatedById))]
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// The target album for uploads.
    /// </summary>
    [ForeignKey(nameof(TargetAlbumId))]
    public Album? TargetAlbum { get; set; }
}
