namespace LibraFoto.Data.Enums
{
    /// <summary>
    /// Types of transitions between slides in the display.
    /// </summary>
    public enum TransitionType
    {
        /// <summary>
        /// Fade transition (crossfade between images).
        /// </summary>
        Fade = 0,

        /// <summary>
        /// Slide transition (slides in from side).
        /// </summary>
        Slide = 1,

        /// <summary>
        /// Ken Burns effect (slow pan and zoom while displaying).
        /// </summary>
        KenBurns = 2
    }
}
