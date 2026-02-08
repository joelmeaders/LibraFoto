using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraFoto.Data.Entities
{
    /// <summary>
    /// Represents a Google Photos Picker session associated with a storage provider.
    /// </summary>
    public class PickerSession
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Storage provider ID that owns this session.
        /// </summary>
        public long ProviderId { get; set; }

        /// <summary>
        /// Google-generated picker session ID.
        /// </summary>
        [MaxLength(256)]
        public required string SessionId { get; set; }

        /// <summary>
        /// Picker URI returned by Google Photos.
        /// </summary>
        [MaxLength(2048)]
        public required string PickerUri { get; set; }

        /// <summary>
        /// Whether media items have been picked for this session.
        /// </summary>
        public bool MediaItemsSet { get; set; }

        /// <summary>
        /// Session creation time (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the session expires (UTC).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Navigation property to storage provider.
        /// </summary>
        [ForeignKey(nameof(ProviderId))]
        public StorageProvider? Provider { get; set; }
    }
}
