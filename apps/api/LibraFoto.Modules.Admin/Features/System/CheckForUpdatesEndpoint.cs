using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.System;

/// <summary>
/// Check for available updates.
/// </summary>
public sealed class CheckForUpdatesEndpoint : EndpointWithoutRequest<Ok<UpdateCheckResponse>>
{
    public override void Configure()
    {
        Get("/api/admin/system/updates");
        Tags("System");
        Summary(s =>
        {
            s.Summary = "Check for available updates";
            s.Description = "Checks the remote repository for available updates.";
        });
    }

    public override async Task<Ok<UpdateCheckResponse>> ExecuteAsync(CancellationToken ct)
    {
        var systemService = Resolve<ISystemService>();
        return await HandleRequestAsync(systemService, ct);
    }

    internal static async Task<Ok<UpdateCheckResponse>> HandleRequestAsync(
        ISystemService systemService,
        CancellationToken ct)
    {
        var result = await systemService.CheckForUpdatesAsync(forceRefresh: false, ct);
        return TypedResults.Ok(result);
    }
}
