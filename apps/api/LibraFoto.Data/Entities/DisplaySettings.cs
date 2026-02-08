using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LibraFoto.Data.Enums;

namespace LibraFoto.Data.Entities
{
    /// <summary>
    /// Represents display settings for the digital picture frame.
    /// Typically there will be one record, but multiple can exist for different displays/schedules.
    /// </summary>
    public class DisplaySettings
    {
        /// <summary>
        /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Name for this display configuration (e.g., "Living Room", "Default").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Duration each slide is displayed in seconds.
        /// </summary>
        public int SlideDuration { get; set; } = 10;

        /// <summary>
        /// Type of transition between slides.
        /// </summary>
        public TransitionType Transition { get; set; } = TransitionType.Fade;

        /// <summary>
        /// Duration of the transition animation in milliseconds.
        /// </summary>
        public int TransitionDuration { get; set; } = 1000;

        /// <summary>
        /// Source type for filtering which photos to display.
        /// </summary>
        public SourceType SourceType { get; set; } = SourceType.All;

        /// <summary>
        /// ID of the source (Album or Tag) when SourceType is not All.
        /// </summary>
        public long? SourceId { get; set; }

        /// <summary>
        /// Whether to shuffle photos randomly.
        /// </summary>
        public bool Shuffle { get; set; } = true;

        /// <summary>
        /// How images should be fitted within the display area.
        /// </summary>
        public ImageFit ImageFit { get; set; } = ImageFit.Contain;

        /// <summary>
        /// Whether this is the active display configuration.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
