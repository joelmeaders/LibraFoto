namespace LibraFoto.Modules.Storage.Models
{
    /// <summary>
    /// Result of image import processing.
    /// </summary>
    public record ImageImportResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? FilePath { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public long FileSize { get; init; }
        public bool WasResized { get; init; }
        public int OriginalWidth { get; init; }
        public int OriginalHeight { get; init; }

        public static ImageImportResult Successful(
            string filePath,
            int width,
            int height,
            long fileSize,
            bool wasResized,
            int originalWidth,
            int originalHeight) => new()
            {
                Success = true,
                FilePath = filePath,
                Width = width,
                Height = height,
                FileSize = fileSize,
                WasResized = wasResized,
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight
            };

        public static ImageImportResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Metadata extracted from an image.
    /// </summary>
    public record ImageMetadata
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public DateTime? DateTaken { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public string? Location { get; init; }
    }
}
