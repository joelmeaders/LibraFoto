using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Slideshow;

/// <summary>
/// Reset the slideshow sequence.
/// </summary>
public sealed class ResetSlideshowSequenceEndpoint : Endpoint<ResetSlideshowSequenceRequest, Ok<ResetResponse>>
{
    public override void Configure()
    {
        Post("/api/display/photos/reset");
        AllowAnonymous();
        Tags("Slideshow");
        Summary(s =>
        {
            s.Summary = "Reset the slideshow sequence";
            s.Description = "Resets the slideshow to the beginning. Useful after settings changes.";
        });
    }

    public override Task<Ok<ResetResponse>> ExecuteAsync(ResetSlideshowSequenceRequest req, CancellationToken ct)
    {
        var slideshowService = Resolve<ISlideshowService>();
        return Task.FromResult(HandleRequest(req, slideshowService));
    }

    internal static Ok<ResetResponse> HandleRequest(
        ResetSlideshowSequenceRequest request,
        ISlideshowService slideshowService)
    {
        slideshowService.ResetSequence(request.SettingsId);
        return TypedResults.Ok(new ResetResponse(true, "Slideshow sequence has been reset."));
    }
}

public sealed class ResetSlideshowSequenceRequest
{
    [QueryParam]
    public long? SettingsId { get; init; }
}

public record ResetResponse(bool Success, string Message);
