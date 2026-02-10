using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Slideshow;

/// <summary>
/// Get the total photo count for the slideshow.
/// </summary>
public sealed class GetDisplayPhotoCountEndpoint : Endpoint<GetDisplayPhotoCountRequest, Ok<PhotoCountResponse>>
{
    public override void Configure()
    {
        Get("/api/display/photos/count");
        AllowAnonymous();
        Tags("Slideshow");
        Summary(s =>
        {
            s.Summary = "Get total photo count";
            s.Description = "Returns the total number of photos available for the current slideshow settings.";
        });
    }

    public override async Task<Ok<PhotoCountResponse>> ExecuteAsync(GetDisplayPhotoCountRequest req, CancellationToken ct)
    {
        var slideshowService = Resolve<ISlideshowService>();
        return await HandleRequestAsync(req, slideshowService, ct);
    }

    internal static async Task<Ok<PhotoCountResponse>> HandleRequestAsync(
        GetDisplayPhotoCountRequest request,
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var count = await slideshowService.GetPhotoCountAsync(request.SettingsId, cancellationToken);

        return TypedResults.Ok(new PhotoCountResponse(count));
    }
}

public sealed class GetDisplayPhotoCountRequest
{
    [QueryParam]
    public long? SettingsId { get; init; }
}

public record PhotoCountResponse(int TotalPhotos);
