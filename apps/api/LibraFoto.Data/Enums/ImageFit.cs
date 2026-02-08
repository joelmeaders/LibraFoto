namespace LibraFoto.Data.Enums
{
    /// <summary>
    /// Specifies how images should be fitted within the display area.
    /// </summary>
    public enum ImageFit
    {
        /// <summary>
        /// Scale image to fit within the display area while maintaining aspect ratio.
        /// The entire image is visible (letterboxing/pillarboxing may occur).
        /// </summary>
        Contain = 0,

        /// <summary>
        /// Scale image to fill the display area while maintaining aspect ratio.
        /// Parts of the image may be cropped if aspect ratios don't match.
        /// </summary>
        Cover = 1
    }
}
