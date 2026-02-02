using LibraFoto.Data;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Endpoints;

/// <summary>
/// Endpoints for serving and generating thumbnails.
/// </summary>
public static class ThumbnailEndpoints
{
    public static IEndpointRouteBuilder MapThumbnailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media/thumbnails");

        group.MapGet("/{photoId:long}", GetThumbnail)
            .WithName("GetThumbnail")
            .WithDescription("Get a 400x400 thumbnail for a photo");

        group.MapPost("/{photoId:long}/generate", GenerateThumbnail)
            .WithName("GenerateThumbnail")
            .WithDescription("Generate thumbnail for a photo")
            .RequireAuthorization();

        group.MapPost("/{photoId:long}/refresh", RefreshThumbnail)
            .WithName("RefreshThumbnail")
            .WithDescription("Delete and regenerate thumbnail for a photo from its source image")
            .RequireAuthorization();

        group.MapPost("/refresh", RefreshThumbnails)
            .WithName("RefreshThumbnails")
            .WithDescription("Delete and regenerate thumbnails for multiple photos")
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Gets a 400x400 thumbnail for a photo.
    /// </summary>
    private static async Task<Results<FileStreamHttpResult, NotFound>> GetThumbnail(
        long photoId,
        IThumbnailService thumbnailService,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] IStorageProviderFactory providerFactory,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        // First, try the ThumbnailService's managed thumbnails
        var stream = thumbnailService.OpenThumbnailStream(photoId);
        if (stream is not null)
        {
            return TypedResults.File(stream, "image/jpeg", enableRangeProcessing: true);
        }

        // Fetch the photo to check for ThumbnailPath or generate thumbnail
        var photo = await dbContext.Photos.FindAsync([photoId], ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        // Check if photo has an explicit ThumbnailPath (e.g., from Google Photos import)
        if (!string.IsNullOrEmpty(photo.ThumbnailPath))
        {
            var thumbnailPath = photo.ThumbnailPath;

            // If it's an absolute path and exists, serve it directly
            if (Path.IsPathRooted(thumbnailPath) && File.Exists(thumbnailPath))
            {
                var fileStream = File.OpenRead(thumbnailPath);
                return TypedResults.File(fileStream, "image/jpeg", enableRangeProcessing: true);
            }

            // If it's a relative path, resolve against storage path
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
            var absoluteThumbnailPath = Path.Combine(storagePath, thumbnailPath);
            if (File.Exists(absoluteThumbnailPath))
            {
                var fileStream = File.OpenRead(absoluteThumbnailPath);
                return TypedResults.File(fileStream, "image/jpeg", enableRangeProcessing: true);
            }
        }

        // Thumbnail doesn't exist, try to generate it
        try
        {
            var dateTaken = photo.DateTaken ?? photo.DateAdded;
            var result = await GenerateThumbnailFromSource(
                photo, thumbnailService, providerFactory, configuration, dateTaken, ct);

            if (result is not null)
            {
                // Update photo's ThumbnailPath for future lookups
                photo.ThumbnailPath = result.Path;
                await dbContext.SaveChangesAsync(ct);

                stream = thumbnailService.OpenThumbnailStream(photoId);
                if (stream is not null)
                {
                    return TypedResults.File(stream, "image/jpeg", enableRangeProcessing: true);
                }
            }
        }
        catch
        {
            // If generation fails, return NotFound
        }

        return TypedResults.NotFound();
    }

    /// <summary>
    /// Generates a thumbnail for a photo.
    /// </summary>
    private static async Task<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>> GenerateThumbnail(
        long photoId,
        [FromBody] GenerateThumbnailRequest request,
        IThumbnailService thumbnailService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            return TypedResults.BadRequest("Source path is required.");
        }

        if (!File.Exists(request.SourcePath))
        {
            return TypedResults.NotFound();
        }

        try
        {
            var dateTaken = request.DateTaken ?? DateTime.UtcNow;
            var result = await thumbnailService.GenerateThumbnailAsync(
                request.SourcePath,
                photoId,
                dateTaken,
                cancellationToken);

            var response = new ThumbnailInfo(
                result.Path ?? "",
                result.Width,
                result.Height,
                result.FileSize
            );

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Failed to generate thumbnail: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes (deletes and regenerates) a thumbnail for a photo from its source image.
    /// </summary>
    private static async Task<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>> RefreshThumbnail(
        long photoId,
        IThumbnailService thumbnailService,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] IStorageProviderFactory providerFactory,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        var photo = await dbContext.Photos.FindAsync([photoId], ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            // Delete existing thumbnail
            thumbnailService.DeleteThumbnails(photoId);

            // Also delete the photo's ThumbnailPath file if it exists and is different
            if (!string.IsNullOrEmpty(photo.ThumbnailPath))
            {
                var existingPath = photo.ThumbnailPath;
                if (Path.IsPathRooted(existingPath) && File.Exists(existingPath))
                {
                    File.Delete(existingPath);
                }
                else
                {
                    var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                    var absolutePath = Path.Combine(storagePath, existingPath);
                    if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                }
            }

            var dateTaken = photo.DateTaken ?? photo.DateAdded;
            var result = await GenerateThumbnailFromSource(
                photo, thumbnailService, providerFactory, configuration, dateTaken, ct);

            if (result is null)
            {
                return TypedResults.BadRequest("Could not access source image to regenerate thumbnail.");
            }

            // Update photo's ThumbnailPath
            photo.ThumbnailPath = result.Path;
            await dbContext.SaveChangesAsync(ct);

            return TypedResults.Ok(new ThumbnailInfo(
                result.Path ?? "",
                result.Width,
                result.Height,
                result.FileSize
            ));
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Failed to refresh thumbnail: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a thumbnail from the photo's source, handling local files, cached files, and provider files.
    /// </summary>
    private static async Task<ThumbnailResult?> GenerateThumbnailFromSource(
        LibraFoto.Data.Entities.Photo photo,
        IThumbnailService thumbnailService,
        IStorageProviderFactory providerFactory,
        IConfiguration configuration,
        DateTime dateTaken,
        CancellationToken ct)
    {
        // First, check if FilePath is an absolute path that exists (cached files from Google Photos, etc.)
        if (Path.IsPathRooted(photo.FilePath) && File.Exists(photo.FilePath))
        {
            return await thumbnailService.GenerateThumbnailAsync(photo.FilePath, photo.Id, dateTaken, ct);
        }

        // For provider-based photos, try to get stream from provider
        if (photo.ProviderId.HasValue)
        {
            var provider = await providerFactory.GetProviderAsync(photo.ProviderId.Value, ct);
            if (provider != null)
            {
                using var sourceStream = await provider.GetFileStreamAsync(photo.ProviderFileId ?? photo.FilePath, ct);
                return await thumbnailService.GenerateThumbnailAsync(sourceStream, photo.Id, dateTaken, ct);
            }
        }

        // Local storage - combine relative path with storage root
        var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
        var absolutePath = Path.Combine(storagePath, photo.FilePath);

        if (File.Exists(absolutePath))
        {
            return await thumbnailService.GenerateThumbnailAsync(absolutePath, photo.Id, dateTaken, ct);
        }

        return null;
    }

    /// <summary>
    /// Refreshes thumbnails for multiple photos.
    /// </summary>
    private static async Task<Results<Ok<RefreshThumbnailsResult>, BadRequest<string>>> RefreshThumbnails(
        [FromBody] RefreshThumbnailsRequest request,
        IThumbnailService thumbnailService,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] IStorageProviderFactory providerFactory,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        if (request.PhotoIds is null || request.PhotoIds.Length == 0)
        {
            return TypedResults.BadRequest("PhotoIds array is required.");
        }

        var succeeded = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var photoId in request.PhotoIds)
        {
            var photo = await dbContext.Photos.FindAsync([photoId], ct);
            if (photo is null)
            {
                failed++;
                errors.Add($"Photo {photoId} not found.");
                continue;
            }

            try
            {
                // Delete existing thumbnail
                thumbnailService.DeleteThumbnails(photoId);

                if (!string.IsNullOrEmpty(photo.ThumbnailPath))
                {
                    var existingPath = photo.ThumbnailPath;
                    if (Path.IsPathRooted(existingPath) && File.Exists(existingPath))
                    {
                        File.Delete(existingPath);
                    }
                    else
                    {
                        var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                        var absolutePath = Path.Combine(storagePath, existingPath);
                        if (File.Exists(absolutePath))
                        {
                            File.Delete(absolutePath);
                        }
                    }
                }

                var dateTaken = photo.DateTaken ?? photo.DateAdded;
                var result = await GenerateThumbnailFromSource(
                    photo, thumbnailService, providerFactory, configuration, dateTaken, ct);

                if (result is not null)
                {
                    photo.ThumbnailPath = result.Path;
                    succeeded++;
                }
                else
                {
                    failed++;
                    errors.Add($"Photo {photoId}: Could not access source image.");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Photo {photoId}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(ct);

        return TypedResults.Ok(new RefreshThumbnailsResult(succeeded, failed, errors.ToArray()));
    }
}

/// <summary>
/// Request to generate thumbnail for a photo.
/// </summary>
public record GenerateThumbnailRequest(string SourcePath, DateTime? DateTaken = null);

/// <summary>
/// Information about a generated thumbnail.
/// </summary>
public record ThumbnailInfo(
    string Path,
    int Width,
    int Height,
    long FileSize
);

/// <summary>
/// Request to refresh thumbnails for multiple photos.
/// </summary>
public record RefreshThumbnailsRequest(long[] PhotoIds);

/// <summary>
/// Result of refreshing multiple thumbnails.
/// </summary>
public record RefreshThumbnailsResult(int Succeeded, int Failed, string[] Errors);
