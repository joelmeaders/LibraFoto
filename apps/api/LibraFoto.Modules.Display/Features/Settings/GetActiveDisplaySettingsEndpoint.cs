using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Get the active display settings.
/// </summary>
public sealed class GetActiveDisplaySettingsEndpoint : EndpointWithoutRequest<Ok<DisplaySettingsDto>>
{
    public override void Configure()
    {
        Get("/api/display/settings");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Get active display settings";
            s.Description = "Returns the currently active display settings configuration.";
        });
    }

    public override async Task<Ok<DisplaySettingsDto>> ExecuteAsync(CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        return await HandleRequestAsync(settingsService, ct);
    }

    internal static async Task<Ok<DisplaySettingsDto>> HandleRequestAsync(
        IDisplaySettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetActiveSettingsAsync(cancellationToken);
        return TypedResults.Ok(settings);
    }
}
