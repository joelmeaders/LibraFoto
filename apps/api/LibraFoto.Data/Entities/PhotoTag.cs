using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities
{
    /// <summary>
    /// Junction table for many-to-many relationship between Photo and Tag.
    /// </summary>
    public class PhotoTag
    {
        /// <summary>
        /// Foreign key to the photo.
        /// </summary>
        public long PhotoId { get; set; }

        /// <summary>
        /// Foreign key to the tag.
        /// </summary>
        public long TagId { get; set; }

        /// <summary>
        /// Date the tag was applied to this photo.
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Navigation properties

        /// <summary>
        /// The photo in this relationship.
        /// </summary>
        [ForeignKey(nameof(PhotoId))]
        public Photo Photo { get; set; } = null!;

        /// <summary>
        /// The tag in this relationship.
        /// </summary>
        [ForeignKey(nameof(TagId))]
        public Tag Tag { get; set; } = null!;
    }
}
