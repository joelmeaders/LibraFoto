using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Slideshow;

/// <summary>
/// Get the next photo in the slideshow.
/// </summary>
public sealed class GetNextPhotoEndpoint : Endpoint<GetNextPhotoRequest, Results<Ok<PhotoDto>, NotFound<ApiError>>>
{
    public override void Configure()
    {
        Get("/api/display/photos/next");
        AllowAnonymous();
        Tags("Slideshow");
        Summary(s =>
        {
            s.Summary = "Get the next photo in the slideshow";
            s.Description = "Returns the next photo based on current display settings. Advances the slideshow sequence.";
        });
    }

    public override async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> ExecuteAsync(GetNextPhotoRequest req, CancellationToken ct)
    {
        var slideshowService = Resolve<ISlideshowService>();
        return await HandleRequestAsync(req, slideshowService, ct);
    }

    internal static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> HandleRequestAsync(
        GetNextPhotoRequest request,
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var photo = await slideshowService.GetNextPhotoAsync(request.SettingsId, cancellationToken);

        if (photo == null)
        {
            return TypedResults.NotFound(new ApiError(
                "NO_PHOTOS_AVAILABLE",
                "No photos are available for the current slideshow settings. Add some photos or adjust your filter settings."));
        }

        return TypedResults.Ok(photo);
    }
}

public sealed class GetNextPhotoRequest
{
    [QueryParam]
    public long? SettingsId { get; init; }
}
