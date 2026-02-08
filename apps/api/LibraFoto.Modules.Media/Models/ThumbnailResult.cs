namespace LibraFoto.Modules.Media.Models
{
    /// <summary>
    /// Result of thumbnail generation operation.
    /// </summary>
    public record ThumbnailResult
    {
        /// <summary>
        /// Whether the thumbnail was generated successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Relative path to the thumbnail file.
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// Absolute path to the thumbnail file.
        /// </summary>
        public string? AbsolutePath { get; init; }

        /// <summary>
        /// Width of the generated thumbnail in pixels.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Height of the generated thumbnail in pixels.
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// File size of the thumbnail in bytes.
        /// </summary>
        public long FileSize { get; init; }

        /// <summary>
        /// Error message if generation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static ThumbnailResult Successful(
            string path,
            string absolutePath,
            int width,
            int height,
            long fileSize) => new()
            {
                Success = true,
                Path = path,
                AbsolutePath = absolutePath,
                Width = width,
                Height = height,
                FileSize = fileSize
            };

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static ThumbnailResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
