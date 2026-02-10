using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a tag for categorizing photos.
/// Tags are stored locally in SQLite (not synced from cloud providers).
/// </summary>
public class Tag
{
    /// <summary>
    /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Tag name (unique).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional color for the tag (hex format, e.g., "#FF5733").
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    // Navigation properties

    /// <summary>
    /// Photos with this tag.
    /// </summary>
    public ICollection<PhotoTag> PhotoTags { get; set; } = [];
}
