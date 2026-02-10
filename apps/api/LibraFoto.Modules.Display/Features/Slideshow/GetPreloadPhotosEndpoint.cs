using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Slideshow;

/// <summary>
/// Get photos for preloading.
/// </summary>
public sealed class GetPreloadPhotosEndpoint : Endpoint<GetPreloadPhotosRequest, Ok<IReadOnlyList<PhotoDto>>>
{
    public override void Configure()
    {
        Get("/api/display/photos/preload");
        AllowAnonymous();
        Tags("Slideshow");
        Summary(s =>
        {
            s.Summary = "Get photos for preloading";
            s.Description = "Returns multiple upcoming photos for frontend caching and preloading.";
        });
    }

    public override async Task<Ok<IReadOnlyList<PhotoDto>>> ExecuteAsync(GetPreloadPhotosRequest req, CancellationToken ct)
    {
        var slideshowService = Resolve<ISlideshowService>();
        return await HandleRequestAsync(req, slideshowService, ct);
    }

    internal static async Task<Ok<IReadOnlyList<PhotoDto>>> HandleRequestAsync(
        GetPreloadPhotosRequest request,
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var preloadCount = Math.Clamp(request.Count ?? 10, 1, 50);
        var photos = await slideshowService.GetPreloadPhotosAsync(preloadCount, request.SettingsId, cancellationToken);

        return TypedResults.Ok(photos);
    }
}

public sealed class GetPreloadPhotosRequest
{
    [QueryParam]
    public int? Count { get; init; }

    [QueryParam]
    public long? SettingsId { get; init; }
}
