using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Setup;

/// <summary>
/// Check if initial setup is required.
/// </summary>
public sealed class GetSetupStatusEndpoint : EndpointWithoutRequest<Ok<SetupStatusResponse>>
{
    public override void Configure()
    {
        Get("/api/setup/status");
        AllowAnonymous();
        Tags("Setup");
        Summary(s =>
        {
            s.Summary = "Check setup status";
            s.Description = "Checks if initial setup is required (no users exist).";
        });
    }

    public override async Task<Ok<SetupStatusResponse>> ExecuteAsync(CancellationToken ct)
    {
        var setupService = Resolve<ISetupService>();
        return await HandleRequestAsync(setupService, ct);
    }

    internal static async Task<Ok<SetupStatusResponse>> HandleRequestAsync(
        ISetupService setupService,
        CancellationToken cancellationToken)
    {
        var status = await setupService.GetSetupStatusAsync(cancellationToken);
        return TypedResults.Ok(status);
    }
}
