using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Modules.Display.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Activate display settings.
/// </summary>
public sealed class ActivateDisplaySettingsEndpoint : EndpointWithoutRequest<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/display/settings/{id:long}/activate");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Activate display settings";
            s.Description = "Sets a display settings configuration as the active one.";
        });
    }

    public override async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> ExecuteAsync(CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        var slideshowService = Resolve<ISlideshowService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, settingsService, slideshowService, ct);
    }

    internal static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> HandleRequestAsync(
        long id,
        IDisplaySettingsService settingsService,
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.SetActiveAsync(id, cancellationToken);
        if (settings == null)
        {
            return TypedResults.NotFound(new ApiError(
                "SETTINGS_NOT_FOUND",
                $"Display settings with ID {id} not found."));
        }

        slideshowService.ResetSequence(null);

        return TypedResults.Ok(settings);
    }
}
