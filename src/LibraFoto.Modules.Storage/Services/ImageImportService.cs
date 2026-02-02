using LibraFoto.Modules.Storage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Services;

/// <summary>
/// Service for processing and importing images (resize, extract metadata).
/// </summary>
public class ImageImportService : IImageImportService
{
    private readonly ILogger<ImageImportService> _logger;

    public ImageImportService(ILogger<ImageImportService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageImportResult> ProcessImageAsync(
        Stream sourceStream,
        string targetPath,
        int maxDimension,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            var originalWidth = image.Width;
            var originalHeight = image.Height;

            // Auto-orient based on EXIF
            image.Mutate(ctx => ctx.AutoOrient());

            // Check if resize is needed
            var needsResize = originalWidth > maxDimension || originalHeight > maxDimension;
            if (needsResize)
            {
                var targetSize = originalWidth > originalHeight
                    ? new Size(maxDimension, (int)(originalHeight * ((double)maxDimension / originalWidth)))
                    : new Size((int)(originalWidth * ((double)maxDimension / originalHeight)), maxDimension);

                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = targetSize,
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

                _logger.LogInformation(
                    "Resized image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}",
                    originalWidth, originalHeight, image.Width, image.Height);
            }

            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Save as JPEG with high quality
            var encoder = new JpegEncoder { Quality = 95 };
            await image.SaveAsync(targetPath, encoder, cancellationToken);

            var fileInfo = new FileInfo(targetPath);

            return ImageImportResult.Successful(
                filePath: targetPath,
                width: image.Width,
                height: image.Height,
                fileSize: fileInfo.Length,
                wasResized: needsResize,
                originalWidth: originalWidth,
                originalHeight: originalHeight
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image for import");
            return ImageImportResult.Failed($"Image processing failed: {ex.Message}");
        }
    }

    public async Task<ImageMetadata?> ExtractMetadataAsync(
        Stream sourceStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            // TODO: Extract EXIF data (DateTaken, GPS coordinates)
            // This requires the MetadataExtractor package
            // For now, just return basic dimensions

            return new ImageMetadata
            {
                Width = image.Width,
                Height = image.Height,
                DateTaken = null,
                Latitude = null,
                Longitude = null,
                Location = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting image metadata");
            return null;
        }
    }
}
