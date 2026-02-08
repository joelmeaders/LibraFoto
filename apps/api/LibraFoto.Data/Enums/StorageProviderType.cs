namespace LibraFoto.Data.Enums
{
    /// <summary>
    /// Types of storage providers for photo sources.
    /// </summary>
    public enum StorageProviderType
    {
        /// <summary>
        /// Local file system storage.
        /// </summary>
        Local = 0,

        /// <summary>
        /// Google Photos API integration.
        /// </summary>
        GooglePhotos = 1,

        /// <summary>
        /// Google Drive API integration.
        /// </summary>
        GoogleDrive = 2,

        /// <summary>
        /// Microsoft OneDrive API integration.
        /// </summary>
        OneDrive = 3
    }
}
