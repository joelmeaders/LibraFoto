using FastEndpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Get all display settings configurations.
/// </summary>
public sealed class GetAllDisplaySettingsEndpoint : EndpointWithoutRequest<Ok<IReadOnlyList<DisplaySettingsDto>>>
{
    public override void Configure()
    {
        Get("/api/display/settings/all");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Get all display settings";
            s.Description = "Returns all display settings configurations.";
        });
    }

    public override async Task<Ok<IReadOnlyList<DisplaySettingsDto>>> ExecuteAsync(CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        return await HandleRequestAsync(settingsService, ct);
    }

    internal static async Task<Ok<IReadOnlyList<DisplaySettingsDto>>> HandleRequestAsync(
        IDisplaySettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAllAsync(cancellationToken);
        return TypedResults.Ok(settings);
    }
}
