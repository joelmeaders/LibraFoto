using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Modules.Media.Services.Shared;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Features.Thumbnails;

/// <summary>
/// Refresh a thumbnail for a photo.
/// </summary>
public sealed class RefreshThumbnailEndpoint : EndpointWithoutRequest<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Post("/api/media/thumbnails/{photoId:long}/refresh");
        Policies("Authenticated");
        Tags("Thumbnails");
        Summary(s =>
        {
            s.Summary = "Delete and regenerate thumbnail for a photo from its source image";
            s.Description = "Delete and regenerate thumbnail for a photo from its source image";
        });
    }

    public override async Task<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>> ExecuteAsync(CancellationToken ct)
    {
        var thumbnailService = Resolve<IThumbnailService>();
        var dbContext = Resolve<LibraFotoDbContext>();
        var configuration = Resolve<IConfiguration>();
        var photoId = Route<long>("photoId");

        var photo = await dbContext.Photos.FindAsync([photoId], ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            thumbnailService.DeleteThumbnails(photoId);

            if (!string.IsNullOrEmpty(photo.ThumbnailPath))
            {
                var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                var absolutePath = Path.Combine(storagePath, photo.ThumbnailPath);
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }

            var dateTaken = photo.DateTaken ?? photo.DateAdded;
            var result = await GenerateThumbnailFromSource(
                photo, thumbnailService, configuration, dateTaken, ct);

            if (result is null)
            {
                return TypedResults.BadRequest("Could not access source image to regenerate thumbnail.");
            }

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
