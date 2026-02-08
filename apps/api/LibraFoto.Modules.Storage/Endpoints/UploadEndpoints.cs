using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.Configuration;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Storage.Endpoints
{
    /// <summary>
    /// Endpoints for file uploads (authenticated and guest).
    /// Uses atomic operations: Save DB record → Upload file → Generate thumbnails → Update DB paths.
    /// On any failure, all artifacts are cleaned up to prevent orphaned files and records.
    /// </summary>
    public static class UploadEndpoints
    {
        /// <summary>
        /// Maximum file size for uploads (100 MB).
        /// </summary>
        private const long MaxFileSize = 100 * 1024 * 1024;

        // TODO: For concurrent uploads at scale, consider implementing optimistic concurrency control
        // or file locking to prevent ID/path conflicts when multiple uploads happen simultaneously.

        /// <summary>
        /// Maps upload endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
        {
            // Authenticated upload endpoints
            var authGroup = app.MapGroup("/api/admin/upload")
                .WithTags("Upload")
                .DisableAntiforgery(); // For file uploads
            // .RequireAuthorization();

            authGroup.MapPost("/", UploadFile)
                .WithName("UploadFile")
                .WithSummary("Upload a file")
                .WithDescription("Uploads a photo or video file to local storage.");

            authGroup.MapPost("/batch", UploadBatch)
                .WithName("UploadBatch")
                .WithSummary("Upload multiple files")
                .WithDescription("Uploads multiple photo or video files to local storage.");

            // Guest upload endpoints (via guest link)
            var guestGroup = app.MapGroup("/api/guest/upload")
                .WithTags("Guest Upload")
                .DisableAntiforgery()
                .AllowAnonymous();

            guestGroup.MapPost("/{linkId}", GuestUpload)
                .WithName("GuestUpload")
                .WithSummary("Upload via guest link")
                .WithDescription("Uploads files using a guest link for authentication.");

            // File access endpoints (for serving files)
            var fileGroup = app.MapGroup("/api/files")
                .WithTags("Files")
                .AllowAnonymous(); // Files may need to be served to display frontend

            fileGroup.MapGet("/{providerId:long}/{**fileId}", GetFile)
                .WithName("GetFile")
                .WithSummary("Get a file")
                .WithDescription("Retrieves a file from a storage provider.");

            return app;
        }

        /// <summary>
        /// Uploads a single file with atomic operations (save, upload, thumbnail, update).
        /// </summary>
        private static async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> UploadFile(
            IFormFile file,
            [FromQuery] long? albumId,
            [FromServices] IStorageProviderFactory providerFactory,
            [FromServices] IMediaScannerService mediaScanner,
            [FromServices] IImageImportService imageImport,
            [FromServices] IConfiguration configuration,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] ILogger<object> logger,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return TypedResults.BadRequest(new ApiError("NO_FILE", "No file was provided"));
            }

            if (file.Length > MaxFileSize)
            {
                return TypedResults.BadRequest(new ApiError("FILE_TOO_LARGE", $"File exceeds maximum size of {MaxFileSize / 1024 / 1024} MB"));
            }

            if (!mediaScanner.IsSupportedMediaFile(file.FileName))
            {
                return TypedResults.BadRequest(new ApiError("UNSUPPORTED_TYPE", $"File type not supported: {Path.GetExtension(file.FileName)}"));
            }

            var provider = await providerFactory.GetOrCreateDefaultLocalProviderAsync(cancellationToken);
            var isImage = mediaScanner.IsSupportedImage(file.FileName);

            // Get configuration
            var maxDimension = configuration.GetValue("Storage:MaxImportDimension", 2560);
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();

            Photo? photo = null;
            string? uploadedFilePath = null;
            var createdThumbnails = new List<string>();

            try
            {
                // STEP 1: Create Photo record in database to get ID
                var dateTaken = DateTime.UtcNow; // TODO: Extract from EXIF
                photo = new Photo
                {
                    Filename = file.FileName, // Will be updated with ID-based name
                    OriginalFilename = file.FileName,
                    FilePath = "", // Will be set after upload
                    FileSize = file.Length,
                    MediaType = isImage ? MediaType.Photo : MediaType.Video,
                    DateAdded = DateTime.UtcNow,
                    DateTaken = dateTaken,
                    ProviderId = provider.ProviderId,
                    Width = 0, // Will be updated
                    Height = 0
                };

                dbContext.Photos.Add(photo);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Created Photo record with ID {PhotoId} for file {FileName}", photo.Id, file.FileName);

                // STEP 2: Upload and process image file using ID-based path
                var yearMonth = Path.Combine(dateTaken.Year.ToString(), dateTaken.Month.ToString("D2"));
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var filename = $"{photo.Id}{extension}";
                var relativePath = Path.Combine("media", yearMonth, filename).Replace('\\', '/');
                uploadedFilePath = Path.Combine(storagePath, "media", yearMonth, filename);

                Directory.CreateDirectory(Path.GetDirectoryName(uploadedFilePath)!);

                if (isImage)
                {
                    // Process image (resize if needed, auto-orient)
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
                    // Videos: just copy directly
                    await using var stream = file.OpenReadStream();
                    await using var fileStream = new FileStream(uploadedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }

                // STEP 3: Generate thumbnail (for images only)
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

                // STEP 4: Update Photo record with file paths and metadata
                photo.Filename = filename;
                photo.FilePath = relativePath;
                photo.ProviderFileId = relativePath;

                // Add to album if specified
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

                // CLEANUP: Remove uploaded file if it exists
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

                // CLEANUP: Remove generated thumbnails
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

                // CLEANUP: Remove database record if it was created
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

        /// <summary>
        /// Uploads multiple files with atomic operations for each file.
        /// Each file upload is independent - failures don't affect other files.
        /// </summary>
        private static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>>> UploadBatch(
            IFormFileCollection files,
            [FromQuery] long? albumId,
            [FromServices] IStorageProviderFactory providerFactory,
            [FromServices] IMediaScannerService mediaScanner,
            [FromServices] IImageImportService imageImport,
            [FromServices] IConfiguration configuration,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] ILogger<object> logger,
            CancellationToken cancellationToken)
        {
            if (files == null || files.Count == 0)
            {
                return TypedResults.BadRequest(new ApiError("NO_FILES", "No files were provided"));
            }

            var results = new List<UploadResult>();
            var successful = 0;
            var failed = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (file.Length == 0)
                {
                    results.Add(UploadResult.Failed("Empty file"));
                    failed++;
                    continue;
                }

                if (file.Length > MaxFileSize)
                {
                    results.Add(UploadResult.Failed($"File exceeds maximum size of {MaxFileSize / 1024 / 1024} MB"));
                    failed++;
                    continue;
                }

                if (!mediaScanner.IsSupportedMediaFile(file.FileName))
                {
                    results.Add(UploadResult.Failed($"Unsupported file type: {Path.GetExtension(file.FileName)}"));
                    failed++;
                    continue;
                }

                // Process single file upload atomically
                var result = await ProcessSingleUploadAsync(
                    file,
                    albumId,
                    providerFactory,
                    mediaScanner,
                    imageImport,
                    configuration,
                    dbContext,
                    logger,
                    cancellationToken);

                results.Add(result);
                if (result.Success)
                {
                    successful++;
                }
                else
                {
                    failed++;
                }
            }

            return TypedResults.Ok(new BatchUploadResult
            {
                TotalFiles = files.Count,
                SuccessfulUploads = successful,
                FailedUploads = failed,
                Results = results
            });
        }

        /// <summary>
        /// Processes a single file upload atomically (save, upload, thumbnail, update).
        /// Returns UploadResult instead of HTTP result for use in batch operations.
        /// </summary>
        private static async Task<UploadResult> ProcessSingleUploadAsync(
            IFormFile file,
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
                // STEP 1: Create Photo record
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

                // STEP 2: Upload and process file
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

                // STEP 3: Generate thumbnail
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

                // STEP 4: Update Photo record
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

                // CLEANUP
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

        /// <summary>
        /// Uploads files via guest link with atomic operations.
        /// </summary>
        private static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>> GuestUpload(
            string linkId,
            IFormFileCollection files,
            [FromServices] IStorageProviderFactory providerFactory,
            [FromServices] IMediaScannerService mediaScanner,
            [FromServices] IImageImportService imageImport,
            [FromServices] IConfiguration configuration,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] ILogger<object> logger,
            CancellationToken cancellationToken)
        {
            // Validate guest link
            var guestLink = await dbContext.GuestLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == linkId, cancellationToken);

            if (guestLink == null)
            {
                return TypedResults.NotFound(new ApiError("LINK_NOT_FOUND", "Guest link not found"));
            }

            if (guestLink.ExpiresAt.HasValue && guestLink.ExpiresAt.Value < DateTime.UtcNow)
            {
                return TypedResults.BadRequest(new ApiError("LINK_EXPIRED", "Guest link has expired"));
            }

            if (guestLink.MaxUploads.HasValue && guestLink.CurrentUploads >= guestLink.MaxUploads.Value)
            {
                return TypedResults.BadRequest(new ApiError("LINK_EXHAUSTED", "Guest link has reached maximum uploads"));
            }

            if (files == null || files.Count == 0)
            {
                return TypedResults.BadRequest(new ApiError("NO_FILES", "No files were provided"));
            }

            var results = new List<UploadResult>();
            var successful = 0;
            var failed = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (file.Length == 0 || file.Length > MaxFileSize || !mediaScanner.IsSupportedMediaFile(file.FileName))
                {
                    results.Add(UploadResult.Failed("Invalid file"));
                    failed++;
                    continue;
                }

                // Use the same atomic upload logic with guest link's target album
                var result = await ProcessSingleUploadAsync(
                    file,
                    guestLink.TargetAlbumId,
                    providerFactory,
                    mediaScanner,
                    imageImport,
                    configuration,
                    dbContext,
                    logger,
                    cancellationToken);

                results.Add(result);
                if (result.Success)
                {
                    successful++;
                }
                else
                {
                    failed++;
                }
            }

            // Update guest link upload count
            if (successful > 0)
            {
                var linkToUpdate = await dbContext.GuestLinks.FindAsync([guestLink.Id], cancellationToken);
                if (linkToUpdate != null)
                {
                    linkToUpdate.CurrentUploads += successful;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return TypedResults.Ok(new BatchUploadResult
            {
                TotalFiles = files.Count,
                SuccessfulUploads = successful,
                FailedUploads = failed,
                Results = results
            });
        }

        /// <summary>
        /// Gets a file from storage.
        /// </summary>
        private static async Task<Results<FileStreamHttpResult, NotFound<ApiError>>> GetFile(
            long providerId,
            string fileId,
            [FromServices] IStorageProviderFactory providerFactory,
            [FromServices] IMediaScannerService mediaScanner,
            CancellationToken cancellationToken)
        {
            var provider = await providerFactory.GetProviderAsync(providerId, cancellationToken);

            if (provider == null)
            {
                return TypedResults.NotFound(new ApiError("PROVIDER_NOT_FOUND", "Storage provider not found"));
            }

            try
            {
                var stream = await provider.GetFileStreamAsync(fileId, cancellationToken);
                var contentType = mediaScanner.GetContentType(fileId);
                var fileName = Path.GetFileName(fileId);

                return TypedResults.File(stream, contentType, fileName);
            }
            catch (FileNotFoundException)
            {
                return TypedResults.NotFound(new ApiError("FILE_NOT_FOUND", $"File not found: {fileId}"));
            }
        }
    }
}
