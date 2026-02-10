using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Features.Thumbnails;

/// <summary>
/// Get a 400x400 thumbnail for a photo.
/// </summary>
public sealed class GetThumbnailEndpoint : EndpointWithoutRequest<Results<FileStreamHttpResult, NotFound>>
{
    public override void Configure()
    {
        Get("/api/media/thumbnails/{photoId:long}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get a 400x400 thumbnail for a photo";
            s.Description = "Get a 400x400 thumbnail for a photo";
        });
    }

    public override async Task<Results<FileStreamHttpResult, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var thumbnailService = Resolve<IThumbnailService>();
        var dbContext = Resolve<LibraFotoDbContext>();
        var configuration = Resolve<IConfiguration>();
        var photoId = Route<long>("photoId");

        var stream = thumbnailService.OpenThumbnailStream(photoId);
        if (stream is not null)
        {
            return TypedResults.File(stream, "image/jpeg", enableRangeProcessing: true);
        }

        var photo = await dbContext.Photos.FindAsync([photoId], ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        if (!string.IsNullOrEmpty(photo.ThumbnailPath))
        {
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
            var absoluteThumbnailPath = Path.Combine(storagePath, photo.ThumbnailPath);
            if (File.Exists(absoluteThumbnailPath))
            {
                var fileStream = File.OpenRead(absoluteThumbnailPath);
                return TypedResults.File(fileStream, "image/jpeg", enableRangeProcessing: true);
            }
        }

        try
        {
            var dateTaken = photo.DateTaken ?? photo.DateAdded;
            var result = await GenerateThumbnailFromSource(
                photo, thumbnailService, configuration, dateTaken, ct);

            if (result is not null)
            {
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
        }

        return TypedResults.NotFound();
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
