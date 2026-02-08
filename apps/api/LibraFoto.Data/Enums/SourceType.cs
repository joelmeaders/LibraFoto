namespace LibraFoto.Data.Enums
{
    /// <summary>
    /// Source type for slideshow content filtering.
    /// </summary>
    public enum SourceType
    {
        /// <summary>
        /// Show all photos from all sources.
        /// </summary>
        All = 0,

        /// <summary>
        /// Show photos from a specific album.
        /// </summary>
        Album = 1,

        /// <summary>
        /// Show photos with a specific tag.
        /// </summary>
        Tag = 2
    }
}
