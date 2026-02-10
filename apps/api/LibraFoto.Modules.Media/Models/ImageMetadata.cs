namespace LibraFoto.Modules.Media.Models;

/// <summary>
/// Extracted metadata from an image or video file.
/// </summary>
public record ImageMetadata
{
    /// <summary>
    /// Whether metadata was extracted successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    // Dimensions

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Orientation value from EXIF (1-8).
    /// </summary>
    public int? Orientation { get; init; }

    // Dates

    /// <summary>
    /// Date and time the photo was taken (from EXIF DateTimeOriginal).
    /// </summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>
    /// Date and time the file was digitized (from EXIF DateTimeDigitized).
    /// </summary>
    public DateTime? DateDigitized { get; init; }

    /// <summary>
    /// Date and time of last modification.
    /// </summary>
    public DateTime? DateModified { get; init; }

    // GPS Location

    /// <summary>
    /// GPS latitude in decimal degrees.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// GPS longitude in decimal degrees.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// GPS altitude in meters.
    /// </summary>
    public double? Altitude { get; init; }

    // Camera Information

    /// <summary>
    /// Camera manufacturer (e.g., "Apple", "Canon").
    /// </summary>
    public string? CameraMake { get; init; }

    /// <summary>
    /// Camera model (e.g., "iPhone 15 Pro", "EOS R5").
    /// </summary>
    public string? CameraModel { get; init; }

    /// <summary>
    /// Lens model if available.
    /// </summary>
    public string? LensModel { get; init; }

    // Exposure Settings

    /// <summary>
    /// Aperture value (f-number, e.g., 2.8).
    /// </summary>
    public double? Aperture { get; init; }

    /// <summary>
    /// Shutter speed in seconds (e.g., 0.001 for 1/1000s).
    /// </summary>
    public double? ShutterSpeed { get; init; }

    /// <summary>
    /// Shutter speed as a readable string (e.g., "1/1000").
    /// </summary>
    public string? ShutterSpeedFormatted { get; init; }

    /// <summary>
    /// ISO sensitivity value.
    /// </summary>
    public int? Iso { get; init; }

    /// <summary>
    /// Focal length in millimeters.
    /// </summary>
    public double? FocalLength { get; init; }

    /// <summary>
    /// 35mm equivalent focal length in millimeters.
    /// </summary>
    public double? FocalLength35mm { get; init; }

    /// <summary>
    /// Flash mode used.
    /// </summary>
    public string? FlashMode { get; init; }

    /// <summary>
    /// Whether flash was fired.
    /// </summary>
    public bool? FlashFired { get; init; }

    /// <summary>
    /// White balance setting.
    /// </summary>
    public string? WhiteBalance { get; init; }

    /// <summary>
    /// Exposure program (e.g., "Aperture Priority", "Manual").
    /// </summary>
    public string? ExposureProgram { get; init; }

    /// <summary>
    /// Metering mode.
    /// </summary>
    public string? MeteringMode { get; init; }

    // Software

    /// <summary>
    /// Software used to create/edit the image.
    /// </summary>
    public string? Software { get; init; }

    // Video-specific

    /// <summary>
    /// Duration in seconds for video files.
    /// </summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Video frame rate (frames per second).
    /// </summary>
    public double? FrameRate { get; init; }

    /// <summary>
    /// Video codec.
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Audio codec.
    /// </summary>
    public string? AudioCodec { get; init; }

    // File Information

    /// <summary>
    /// MIME content type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// File format (e.g., "JPEG", "PNG", "HEIC").
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Color space (e.g., "sRGB", "Adobe RGB").
    /// </summary>
    public string? ColorSpace { get; init; }

    /// <summary>
    /// Bit depth per channel.
    /// </summary>
    public int? BitDepth { get; init; }

    /// <summary>
    /// Creates a successful result with extracted metadata.
    /// </summary>
    public static ImageMetadata Successful(Action<ImageMetadataBuilder> configure)
    {
        var builder = new ImageMetadataBuilder();
        configure(builder);
        return builder.Build() with { Success = true };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ImageMetadata Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Builder for creating ImageMetadata instances.
/// </summary>
public class ImageMetadataBuilder
{
    private ImageMetadata _metadata = new();

    public ImageMetadataBuilder WithDimensions(int? width, int? height, int? orientation = null)
    {
        _metadata = _metadata with { Width = width, Height = height, Orientation = orientation };
        return this;
    }

    public ImageMetadataBuilder WithDates(DateTime? dateTaken, DateTime? dateDigitized = null, DateTime? dateModified = null)
    {
        _metadata = _metadata with { DateTaken = dateTaken, DateDigitized = dateDigitized, DateModified = dateModified };
        return this;
    }

    public ImageMetadataBuilder WithGps(double? latitude, double? longitude, double? altitude = null)
    {
        _metadata = _metadata with { Latitude = latitude, Longitude = longitude, Altitude = altitude };
        return this;
    }

    public ImageMetadataBuilder WithCamera(string? make, string? model, string? lens = null)
    {
        _metadata = _metadata with { CameraMake = make, CameraModel = model, LensModel = lens };
        return this;
    }

    public ImageMetadataBuilder WithExposure(
        double? aperture = null,
        double? shutterSpeed = null,
        string? shutterSpeedFormatted = null,
        int? iso = null,
        double? focalLength = null,
        double? focalLength35mm = null)
    {
        _metadata = _metadata with
        {
            Aperture = aperture,
            ShutterSpeed = shutterSpeed,
            ShutterSpeedFormatted = shutterSpeedFormatted,
            Iso = iso,
            FocalLength = focalLength,
            FocalLength35mm = focalLength35mm
        };
        return this;
    }

    public ImageMetadataBuilder WithFlash(string? mode, bool? fired)
    {
        _metadata = _metadata with { FlashMode = mode, FlashFired = fired };
        return this;
    }

    public ImageMetadataBuilder WithOtherSettings(
        string? whiteBalance = null,
        string? exposureProgram = null,
        string? meteringMode = null)
    {
        _metadata = _metadata with
        {
            WhiteBalance = whiteBalance,
            ExposureProgram = exposureProgram,
            MeteringMode = meteringMode
        };
        return this;
    }

    public ImageMetadataBuilder WithSoftware(string? software)
    {
        _metadata = _metadata with { Software = software };
        return this;
    }

    public ImageMetadataBuilder WithVideo(double? duration, double? frameRate, string? videoCodec, string? audioCodec)
    {
        _metadata = _metadata with
        {
            Duration = duration,
            FrameRate = frameRate,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec
        };
        return this;
    }

    public ImageMetadataBuilder WithFormat(string? contentType, string? format, string? colorSpace = null, int? bitDepth = null)
    {
        _metadata = _metadata with
        {
            ContentType = contentType,
            Format = format,
            ColorSpace = colorSpace,
            BitDepth = bitDepth
        };
        return this;
    }

    public ImageMetadata Build() => _metadata;
}
