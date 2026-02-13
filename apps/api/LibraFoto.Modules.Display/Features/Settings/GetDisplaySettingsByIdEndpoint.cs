using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Modules.Display.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Get display settings by ID.
/// </summary>
public sealed class GetDisplaySettingsByIdEndpoint : EndpointWithoutRequest<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>>
{
    public override void Configure()
    {
        Get("/api/display/settings/{id:long}");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Get display settings by ID";
            s.Description = "Returns a specific display settings configuration.";
        });
    }

    public override async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> ExecuteAsync(CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, settingsService, ct);
    }

    internal static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> HandleRequestAsync(
        long id,
        IDisplaySettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetByIdAsync(id, cancellationToken);

        if (settings == null)
        {
            return TypedResults.NotFound(new ApiError(
                "SETTINGS_NOT_FOUND",
                $"Display settings with ID {id} not found."));
        }

        return TypedResults.Ok(settings);
    }
}
