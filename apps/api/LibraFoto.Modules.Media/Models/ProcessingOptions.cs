namespace LibraFoto.Modules.Media.Models
{
    /// <summary>
    /// Options for image processing operations.
    /// </summary>
    public record ProcessingOptions
    {
        /// <summary>
        /// Target width for resizing. Null to preserve aspect ratio based on height.
        /// </summary>
        public int? Width { get; init; }

        /// <summary>
        /// Target height for resizing. Null to preserve aspect ratio based on width.
        /// </summary>
        public int? Height { get; init; }

        /// <summary>
        /// Maximum dimension (constrains both width and height while preserving aspect ratio).
        /// </summary>
        public int? MaxDimension { get; init; }

        /// <summary>
        /// Resize mode determining how the image fits the target dimensions.
        /// </summary>
        public ImageResizeMode ResizeMode { get; init; } = ImageResizeMode.Max;

        /// <summary>
        /// Quality level for JPEG output (1-100). Default is 85.
        /// </summary>
        public int JpegQuality { get; init; } = 85;

        /// <summary>
        /// Quality level for WebP output (1-100). Default is 80.
        /// </summary>
        public int WebPQuality { get; init; } = 80;

        /// <summary>
        /// Whether to automatically orient the image based on EXIF data.
        /// </summary>
        public bool AutoOrient { get; init; } = true;

        /// <summary>
        /// Rotation angle in degrees (clockwise). Valid values: 0, 90, 180, 270.
        /// </summary>
        public int RotationDegrees { get; init; }

        /// <summary>
        /// Whether to flip horizontally.
        /// </summary>
        public bool FlipHorizontal { get; init; }

        /// <summary>
        /// Whether to flip vertically.
        /// </summary>
        public bool FlipVertical { get; init; }

        /// <summary>
        /// Target output format. Null to preserve original format.
        /// </summary>
        public ImageOutputFormat? OutputFormat { get; init; }

        /// <summary>
        /// Whether to strip all metadata from the output.
        /// </summary>
        public bool StripMetadata { get; init; }

        /// <summary>
        /// Background color for images with transparency when converting to non-transparent formats.
        /// Default is white.
        /// </summary>
        public string BackgroundColor { get; init; } = "#FFFFFF";

        /// <summary>
        /// Whether to use progressive encoding for JPEG.
        /// </summary>
        public bool Progressive { get; init; } = true;

        /// <summary>
        /// Creates default options for thumbnail generation (400x400).
        /// </summary>
        public static ProcessingOptions ForThumbnail() => new()
        {
            MaxDimension = 400,
            ResizeMode = ImageResizeMode.Max,
            JpegQuality = 85,
            AutoOrient = true,
            OutputFormat = ImageOutputFormat.Jpeg,
            StripMetadata = true,
            Progressive = false
        };

        /// <summary>
        /// Creates default options for web display.
        /// </summary>
        public static ProcessingOptions ForWebDisplay(int maxDimension = 1920) => new()
        {
            MaxDimension = maxDimension,
            ResizeMode = ImageResizeMode.Max,
            JpegQuality = 85,
            AutoOrient = true,
            Progressive = true
        };

        /// <summary>
        /// Creates default options for full quality processing.
        /// </summary>
        public static ProcessingOptions FullQuality() => new()
        {
            JpegQuality = 95,
            AutoOrient = true,
            Progressive = true
        };
    }

    /// <summary>
    /// Resize mode for image processing.
    /// </summary>
    public enum ImageResizeMode
    {
        /// <summary>
        /// Scales the image down while maintaining aspect ratio so that the entire image
        /// fits within the target dimensions. The resulting image may be smaller than
        /// the target in one dimension.
        /// </summary>
        Max,

        /// <summary>
        /// Scales the image to fill the target dimensions, cropping if necessary.
        /// The resulting image will be exactly the target size.
        /// </summary>
        Crop,

        /// <summary>
        /// Scales the image to fit within the target dimensions, padding with background
        /// color if necessary. The resulting image will be exactly the target size.
        /// </summary>
        Pad,

        /// <summary>
        /// Scales the image to the exact target dimensions, potentially distorting the image.
        /// </summary>
        Stretch,

        /// <summary>
        /// Scales the image so that the shortest side fits the target,
        /// cropping the longer side to center.
        /// </summary>
        Fill
    }

    /// <summary>
    /// Output formats for image processing.
    /// </summary>
    public enum ImageOutputFormat
    {
        /// <summary>
        /// JPEG format - best for photographs.
        /// </summary>
        Jpeg,

        /// <summary>
        /// PNG format - best for images requiring transparency.
        /// </summary>
        Png,

        /// <summary>
        /// WebP format - modern format with good compression and quality.
        /// </summary>
        WebP,

        /// <summary>
        /// GIF format - for animations or simple graphics.
        /// </summary>
        Gif,

        /// <summary>
        /// BMP format - uncompressed bitmap.
        /// </summary>
        Bmp
    }
}
