using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities
{
    /// <summary>
    /// Represents an album for organizing photos.
    /// </summary>
    public class Album
    {
        /// <summary>
        /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Album name.
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the album.
        /// </summary>
        [MaxLength(1024)]
        public string? Description { get; set; }

        /// <summary>
        /// Foreign key to the cover photo.
        /// </summary>
        public long? CoverPhotoId { get; set; }

        /// <summary>
        /// Date the album was created.
        /// </summary>
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sort order for displaying albums.
        /// </summary>
        public int SortOrder { get; set; }

        // Navigation properties

        /// <summary>
        /// The cover photo for this album.
        /// </summary>
        [ForeignKey(nameof(CoverPhotoId))]
        public Photo? CoverPhoto { get; set; }

        /// <summary>
        /// Photos in this album.
        /// </summary>
        public ICollection<PhotoAlbum> PhotoAlbums { get; set; } = [];
    }
}
