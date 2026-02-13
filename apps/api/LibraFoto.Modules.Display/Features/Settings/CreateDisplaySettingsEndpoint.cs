using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Modules.Display.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Create display settings.
/// </summary>
public sealed class CreateDisplaySettingsEndpoint : Endpoint<UpdateDisplaySettingsRequest, Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/display/settings");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Create display settings";
            s.Description = "Creates a new display settings configuration.";
        });
    }

    public override async Task<Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>> ExecuteAsync(
        UpdateDisplaySettingsRequest req,
        CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        return await HandleRequestAsync(req, settingsService, ct);
    }

    internal static async Task<Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>> HandleRequestAsync(
        UpdateDisplaySettingsRequest request,
        IDisplaySettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (request.SlideDuration.HasValue && request.SlideDuration.Value < 1)
        {
            return TypedResults.BadRequest(new ApiError(
                "VALIDATION_ERROR",
                "Slide duration must be at least 1 second."));
        }

        if (request.TransitionDuration.HasValue && request.TransitionDuration.Value < 0)
        {
            return TypedResults.BadRequest(new ApiError(
                "VALIDATION_ERROR",
                "Transition duration cannot be negative."));
        }

        var settings = await settingsService.CreateAsync(request, cancellationToken);

        return TypedResults.Created($"/api/display/settings/{settings.Id}", settings);
    }
}
