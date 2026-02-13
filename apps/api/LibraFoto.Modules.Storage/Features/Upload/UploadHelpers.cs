using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Modules.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Storage.Features.Upload;

internal static class UploadHelpers
{
    internal const long MaxFileSize = 100 * 1024 * 1024;

    internal static async Task<UploadResult> ProcessSingleUploadAsync(
        Microsoft.AspNetCore.Http.IFormFile file,
        long? albumId,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        IImageImportService imageImport,
        IConfiguration configuration,
        LibraFotoDbContext dbContext,
        ILogger<object> logger,
        CancellationToken cancellationToken)
    {
        var provider = await providerFactory.GetOrCreateDefaultLocalProviderAsync(cancellationToken);
        var isImage = mediaScanner.IsSupportedImage(file.FileName);

        var maxDimension = configuration.GetValue("Storage:MaxImportDimension", 2560);
        var storagePath = configuration["Storage:LocalPath"] ?? ".\\photos";

        Photo? photo = null;
        string? uploadedFilePath = null;
        var createdThumbnails = new List<string>();

        try
        {
            var dateTaken = DateTime.UtcNow;
            photo = new Photo
            {
                Filename = file.FileName,
                OriginalFilename = file.FileName,
                FilePath = "",
                FileSize = file.Length,
                MediaType = isImage ? MediaType.Photo : MediaType.Video,
                DateAdded = DateTime.UtcNow,
                DateTaken = dateTaken,
                ProviderId = provider.ProviderId,
                Width = 0,
                Height = 0
            };

            dbContext.Photos.Add(photo);
            await dbContext.SaveChangesAsync(cancellationToken);

            var yearMonth = Path.Combine(dateTaken.Year.ToString(), dateTaken.Month.ToString("D2"));
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var filename = $"{photo.Id}{extension}";
            var relativePath = Path.Combine("media", yearMonth, filename).Replace('\\', '/');
            uploadedFilePath = Path.Combine(storagePath, "media", yearMonth, filename);

            Directory.CreateDirectory(Path.GetDirectoryName(uploadedFilePath)!);

            if (isImage)
            {
                await using var stream = file.OpenReadStream();
                var importResult = await imageImport.ProcessImageAsync(stream, uploadedFilePath, maxDimension, cancellationToken);

                if (!importResult.Success)
                {
                    throw new Exception(importResult.ErrorMessage ?? "Image processing failed");
                }

                photo.Width = importResult.Width;
                photo.Height = importResult.Height;
                photo.FileSize = importResult.FileSize;
            }
            else
            {
                await using var stream = file.OpenReadStream();
                await using var fileStream = new FileStream(uploadedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            if (isImage)
            {
                var thumbnailBasePath = Path.Combine(storagePath, ".thumbnails", yearMonth);
                Directory.CreateDirectory(thumbnailBasePath);

                try
                {
                    using var sourceImage = await Image.LoadAsync(uploadedFilePath, cancellationToken);
                    sourceImage.Mutate(ctx => ctx.AutoOrient());

                    using var thumbnail = sourceImage.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));

                    var thumbnailPath = Path.Combine(thumbnailBasePath, $"{photo.Id}.jpg");
                    var encoder = new JpegEncoder { Quality = 85 };
                    await thumbnail.SaveAsync(thumbnailPath, encoder, cancellationToken);

                    createdThumbnails.Add(thumbnailPath);

                    var relativeThumbnailPath = Path.Combine(".thumbnails", yearMonth, $"{photo.Id}.jpg").Replace('\\', '/');
                    photo.ThumbnailPath = relativeThumbnailPath;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate thumbnail for photo {PhotoId}", photo.Id);
                }
            }

            photo.Filename = filename;
            photo.FilePath = relativePath;
            photo.ProviderFileId = relativePath;

            if (albumId.HasValue)
            {
                var album = await dbContext.Albums.FindAsync([albumId.Value], cancellationToken);
                if (album != null)
                {
                    dbContext.PhotoAlbums.Add(new PhotoAlbum
                    {
                        Photo = photo,
                        AlbumId = albumId.Value,
                        SortOrder = 0
                    });
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Successfully uploaded {FileName} as photo {PhotoId}", file.FileName, photo.Id);

            return new UploadResult
            {
                Success = true,
                PhotoId = photo.Id,
                FileId = relativePath,
                FileName = filename,
                FilePath = relativePath,
                FileSize = photo.FileSize,
                ContentType = file.ContentType
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for {FileName}, cleaning up", file.FileName);

            if (!string.IsNullOrEmpty(uploadedFilePath) && File.Exists(uploadedFilePath))
            {
                try
                { File.Delete(uploadedFilePath); }
                catch { }
            }

            foreach (var thumbnailPath in createdThumbnails)
            {
                try
                {
                    if (File.Exists(thumbnailPath))
                    {
                        File.Delete(thumbnailPath);
                    }
                }
                catch { }
            }

            if (photo != null && photo.Id > 0)
            {
                try
                {
                    dbContext.Photos.Remove(photo);
                    await dbContext.SaveChangesAsync(CancellationToken.None);
                }
                catch { }
            }

            return UploadResult.Failed($"Upload failed: {ex.Message}");
        }
    }
}
