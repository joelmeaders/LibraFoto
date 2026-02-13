using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.System;

/// <summary>
/// Force check for updates.
/// </summary>
public sealed class ForceCheckForUpdatesEndpoint : EndpointWithoutRequest<Ok<UpdateCheckResponse>>
{
    public override void Configure()
    {
        Post("/api/admin/system/updates/check");
        Tags("System");
        Summary(s =>
        {
            s.Summary = "Force check for updates";
            s.Description = "Forces a fresh check for updates, bypassing the cache.";
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
        var result = await systemService.CheckForUpdatesAsync(forceRefresh: true, ct);
        return TypedResults.Ok(result);
    }
}
