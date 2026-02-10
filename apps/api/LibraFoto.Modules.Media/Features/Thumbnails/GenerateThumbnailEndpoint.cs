using FastEndpoints;
using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Media.Features.Thumbnails;

/// <summary>
/// Generate thumbnail for a photo.
/// </summary>
public sealed class GenerateThumbnailEndpoint : Endpoint<GenerateThumbnailRequest, Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Post("/api/media/thumbnails/{photoId:long}/generate");
        Policies("Authenticated");
        Tags("Thumbnails");
        Summary(s =>
        {
            s.Summary = "Generate thumbnail for a photo";
            s.Description = "Generate thumbnail for a photo";
        });
    }

    public override async Task<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>> ExecuteAsync(
        GenerateThumbnailRequest req,
        CancellationToken ct)
    {
        var thumbnailService = Resolve<IThumbnailService>();
        var photoId = Route<long>("photoId");
        return await HandleRequestAsync(photoId, req, thumbnailService, ct);
    }

    internal static async Task<Results<Ok<ThumbnailInfo>, NotFound, BadRequest<string>>> HandleRequestAsync(
        long photoId,
        GenerateThumbnailRequest request,
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
}

public record GenerateThumbnailRequest(string SourcePath, DateTime? DateTaken = null);

public record ThumbnailInfo(
    string Path,
    int Width,
    int Height,
    long FileSize
);
