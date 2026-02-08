using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Display.Models
{
    /// <summary>
    /// Display settings data transfer object for the frontend.
    /// Contains all settings needed to configure the slideshow display.
    /// </summary>
    public record DisplaySettingsDto
    {
        /// <summary>
        /// Settings ID.
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// Name of this display configuration.
        /// </summary>
        public string Name { get; init; } = "Default";

        /// <summary>
        /// Duration each slide is displayed in seconds.
        /// </summary>
        public int SlideDuration { get; init; } = 10;

        /// <summary>
        /// Type of transition between slides.
        /// </summary>
        public TransitionType Transition { get; init; } = TransitionType.Fade;

        /// <summary>
        /// Duration of the transition animation in milliseconds.
        /// </summary>
        public int TransitionDuration { get; init; } = 1000;

        /// <summary>
        /// Source type for filtering which photos to display.
        /// </summary>
        public SourceType SourceType { get; init; } = SourceType.All;

        /// <summary>
        /// ID of the source (Album or Tag) when SourceType is not All.
        /// </summary>
        public long? SourceId { get; init; }

        /// <summary>
        /// Whether to shuffle photos randomly.
        /// </summary>
        public bool Shuffle { get; init; } = true;

        /// <summary>
        /// How images should be fitted within the display area.
        /// </summary>
        public ImageFit ImageFit { get; init; } = ImageFit.Contain;
    }

    /// <summary>
    /// Request to update display settings.
    /// </summary>
    public record UpdateDisplaySettingsRequest
    {
        /// <summary>
        /// Name of this display configuration.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Duration each slide is displayed in seconds.
        /// </summary>
        public int? SlideDuration { get; init; }

        /// <summary>
        /// Type of transition between slides.
        /// </summary>
        public TransitionType? Transition { get; init; }

        /// <summary>
        /// Duration of the transition animation in milliseconds.
        /// </summary>
        public int? TransitionDuration { get; init; }

        /// <summary>
        /// Source type for filtering which photos to display.
        /// </summary>
        public SourceType? SourceType { get; init; }

        /// <summary>
        /// ID of the source (Album or Tag) when SourceType is not All.
        /// </summary>
        public long? SourceId { get; init; }

        /// <summary>
        /// Whether to shuffle photos randomly.
        /// </summary>
        public bool? Shuffle { get; init; }

        /// <summary>
        /// How images should be fitted within the display area.
        /// </summary>
        public ImageFit? ImageFit { get; init; }
    }
}
