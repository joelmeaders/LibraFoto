using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Update display settings.
/// </summary>
public sealed class UpdateDisplaySettingsEndpoint : Endpoint<UpdateDisplaySettingsRequest, Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Put("/api/display/settings/{id:long}");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Update display settings";
            s.Description = "Updates an existing display settings configuration.";
        });
    }

    public override async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        UpdateDisplaySettingsRequest req,
        CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        var slideshowService = Resolve<ISlideshowService>();
        var id = Route<long>("id");

        return await HandleRequestAsync(id, req, settingsService, slideshowService, ct);
    }

    internal static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long id,
        UpdateDisplaySettingsRequest request,
        IDisplaySettingsService settingsService,
        ISlideshowService slideshowService,
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

        var settings = await settingsService.UpdateAsync(id, request, cancellationToken);

        if (settings == null)
        {
            return TypedResults.NotFound(new ApiError(
                "SETTINGS_NOT_FOUND",
                $"Display settings with ID {id} not found."));
        }

        slideshowService.ResetSequence(id);

        return TypedResults.Ok(settings);
    }
}
