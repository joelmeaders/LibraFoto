using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.Configuration;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Storage.Features.Upload;

/// <summary>
/// Upload a single file.
/// </summary>
public sealed class UploadFileEndpoint : Endpoint<UploadFileRequest, Results<Ok<UploadResult>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/admin/upload");
        AllowFileUploads();
        Tags("Upload");
        Summary(s =>
        {
            s.Summary = "Upload a file";
            s.Description = "Uploads a photo or video file to local storage.";
        });
        // TODO: Re-enable authorization for /api/admin/upload when auth enforcement is ready.
    }

    public override async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> ExecuteAsync(
        UploadFileRequest req,
        CancellationToken ct)
    {
        var providerFactory = Resolve<IStorageProviderFactory>();
        var mediaScanner = Resolve<IMediaScannerService>();
        var imageImport = Resolve<IImageImportService>();
        var configuration = Resolve<IConfiguration>();
        var dbContext = Resolve<LibraFotoDbContext>();
        var logger = Resolve<ILogger<object>>();
        var file = req.File ?? Files.FirstOrDefault();

        return await HandleRequestAsync(
            file,
            req.AlbumId,
            providerFactory,
            mediaScanner,
            imageImport,
            configuration,
            dbContext,
            logger,
            ct);
    }

    internal static async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> HandleRequestAsync(
        IFormFile? file,
        long? albumId,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        IImageImportService imageImport,
        IConfiguration configuration,
        LibraFotoDbContext dbContext,
        ILogger<object> logger,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return TypedResults.BadRequest(new ApiError("NO_FILE", "No file was provided"));
        }

        if (file.Length > UploadHelpers.MaxFileSize)
        {
            return TypedResults.BadRequest(new ApiError("FILE_TOO_LARGE", $"File exceeds maximum size of {UploadHelpers.MaxFileSize / 1024 / 1024} MB"));
        }

        if (!mediaScanner.IsSupportedMediaFile(file.FileName))
        {
            return TypedResults.BadRequest(new ApiError("UNSUPPORTED_TYPE", $"File type not supported: {Path.GetExtension(file.FileName)}"));
        }

        var provider = await providerFactory.GetOrCreateDefaultLocalProviderAsync(cancellationToken);
        var isImage = mediaScanner.IsSupportedImage(file.FileName);

        var maxDimension = configuration.GetValue("Storage:MaxImportDimension", 2560);
        var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();

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

            logger.LogInformation("Created Photo record with ID {PhotoId} for file {FileName}", photo.Id, file.FileName);

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

                logger.LogInformation("Processed image {PhotoId}: {Width}x{Height}, resized={Resized}",
                    photo.Id, photo.Width, photo.Height, importResult.WasResized);
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

                    logger.LogInformation("Generated thumbnail for photo {PhotoId}: {Path}",
                        photo.Id, relativeThumbnailPath);
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

            logger.LogInformation("Successfully uploaded file {FileName} as photo {PhotoId} at {FilePath}",
                file.FileName, photo.Id, relativePath);

            return TypedResults.Ok(new UploadResult
            {
                Success = true,
                PhotoId = photo.Id,
                FileId = relativePath,
                FileName = filename,
                FilePath = relativePath,
                FileSize = photo.FileSize,
                ContentType = file.ContentType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for file {FileName}, cleaning up artifacts", file.FileName);

            if (!string.IsNullOrEmpty(uploadedFilePath) && File.Exists(uploadedFilePath))
            {
                try
                {
                    File.Delete(uploadedFilePath);
                    logger.LogInformation("Deleted uploaded file {FilePath}", uploadedFilePath);
                }
                catch (Exception deleteEx)
                {
                    logger.LogError(deleteEx, "Failed to delete uploaded file {FilePath}", uploadedFilePath);
                }
            }

            foreach (var thumbnailPath in createdThumbnails)
            {
                try
                {
                    if (File.Exists(thumbnailPath))
                    {
                        File.Delete(thumbnailPath);
                        logger.LogInformation("Deleted thumbnail {ThumbnailPath}", thumbnailPath);
                    }
                }
                catch (Exception deleteEx)
                {
                    logger.LogError(deleteEx, "Failed to delete thumbnail {ThumbnailPath}", thumbnailPath);
                }
            }

            if (photo != null && photo.Id > 0)
            {
                try
                {
                    dbContext.Photos.Remove(photo);
                    await dbContext.SaveChangesAsync(CancellationToken.None);
                    logger.LogInformation("Deleted Photo record {PhotoId}", photo.Id);
                }
                catch (Exception deleteEx)
                {
                    logger.LogError(deleteEx, "Failed to delete Photo record {PhotoId}", photo.Id);
                }
            }

            return TypedResults.BadRequest(new ApiError("UPLOAD_FAILED", $"Upload failed: {ex.Message}"));
        }
    }
}

public sealed class UploadFileRequest
{
    public IFormFile? File { get; init; }

    [QueryParam]
    public long? AlbumId { get; init; }
}
