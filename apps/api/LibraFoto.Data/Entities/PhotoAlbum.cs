using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Junction table for many-to-many relationship between Photo and Album.
/// </summary>
public class PhotoAlbum
{
    /// <summary>
    /// Foreign key to the photo.
    /// </summary>
    public long PhotoId { get; set; }

    /// <summary>
    /// Foreign key to the album.
    /// </summary>
    public long AlbumId { get; set; }

    /// <summary>
    /// Sort order of the photo within the album.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Date the photo was added to this album.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>
    /// The photo in this relationship.
    /// </summary>
    [ForeignKey(nameof(PhotoId))]
    public Photo Photo { get; set; } = null!;

    /// <summary>
    /// The album in this relationship.
    /// </summary>
    [ForeignKey(nameof(AlbumId))]
    public Album Album { get; set; } = null!;
}
