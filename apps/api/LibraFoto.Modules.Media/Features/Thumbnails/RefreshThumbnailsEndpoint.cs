using FastEndpoints;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Modules.Media.Services.Repositories;
using LibraFoto.Modules.Media.Services.Shared;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Features.Thumbnails;

/// <summary>
/// Refresh thumbnails for multiple photos.
/// </summary>
public sealed class RefreshThumbnailsEndpoint : Endpoint<RefreshThumbnailsRequest, Results<Ok<RefreshThumbnailsResult>, BadRequest<string>>>
{
    public override void Configure()
    {
        Post("/api/media/thumbnails/refresh");
        Policies("Authenticated");
        Tags("Thumbnails");
        Summary(s =>
        {
            s.Summary = "Delete and regenerate thumbnails for multiple photos";
            s.Description = "Delete and regenerate thumbnails for multiple photos";
        });
    }

    public override async Task<Results<Ok<RefreshThumbnailsResult>, BadRequest<string>>> ExecuteAsync(
        RefreshThumbnailsRequest req,
        CancellationToken ct)
    {
        var thumbnailService = Resolve<IThumbnailService>();
        var mediaPhotoRepository = Resolve<IMediaPhotoRepository>();
        var configuration = Resolve<IConfiguration>();

        if (req.PhotoIds is null || req.PhotoIds.Length == 0)
        {
            return TypedResults.BadRequest("PhotoIds array is required.");
        }

        var succeeded = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var photoId in req.PhotoIds)
        {
            var photo = await mediaPhotoRepository.GetPhotoByIdAsync(photoId, ct);
            if (photo is null)
            {
                failed++;
                errors.Add($"Photo {photoId} not found.");
                continue;
            }

            try
            {
                thumbnailService.DeleteThumbnails(photoId);

                if (!string.IsNullOrEmpty(photo.ThumbnailPath))
                {
                    var storagePath2 = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                    var absolutePath2 = Path.Combine(storagePath2, photo.ThumbnailPath);
                    if (File.Exists(absolutePath2))
                    {
                        File.Delete(absolutePath2);
                    }
                }

                var dateTaken = photo.DateTaken ?? photo.DateAdded;
                var result = await GenerateThumbnailFromSource(
                    photo, thumbnailService, configuration, dateTaken, ct);

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

        await mediaPhotoRepository.SaveChangesAsync(ct);

        return TypedResults.Ok(new RefreshThumbnailsResult(succeeded, failed, errors.ToArray()));
    }

    private static async Task<ThumbnailResult?> GenerateThumbnailFromSource(
        LibraFoto.Data.Entities.Photo photo,
        IThumbnailService thumbnailService,
        IConfiguration configuration,
        DateTime dateTaken,
        CancellationToken ct)
    {
        var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
        var absolutePath = Path.Combine(storagePath, photo.FilePath);

        if (File.Exists(absolutePath))
        {
            return await thumbnailService.GenerateThumbnailAsync(absolutePath, photo.Id, dateTaken, ct);
        }

        return null;
    }
}

public record RefreshThumbnailsRequest(long[] PhotoIds);

public record RefreshThumbnailsResult(int Succeeded, int Failed, string[] Errors);
