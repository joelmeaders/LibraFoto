using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Slideshow;

/// <summary>
/// Get the current photo in the slideshow.
/// </summary>
public sealed class GetCurrentPhotoEndpoint : Endpoint<GetCurrentPhotoRequest, Results<Ok<PhotoDto>, NotFound<ApiError>>>
{
    public override void Configure()
    {
        Get("/api/display/photos/current");
        AllowAnonymous();
        Tags("Slideshow");
        Summary(s =>
        {
            s.Summary = "Get the current photo";
            s.Description = "Returns the currently displayed photo without advancing the sequence.";
        });
    }

    public override async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> ExecuteAsync(GetCurrentPhotoRequest req, CancellationToken ct)
    {
        var slideshowService = Resolve<ISlideshowService>();
        return await HandleRequestAsync(req, slideshowService, ct);
    }

    internal static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> HandleRequestAsync(
        GetCurrentPhotoRequest request,
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var photo = await slideshowService.GetCurrentPhotoAsync(request.SettingsId, cancellationToken);

        if (photo == null)
        {
            return TypedResults.NotFound(new ApiError(
                "NO_PHOTOS_AVAILABLE",
                "No photos are available for the current slideshow settings."));
        }

        return TypedResults.Ok(photo);
    }
}

public sealed class GetCurrentPhotoRequest
{
    [QueryParam]
    public long? SettingsId { get; init; }
}
