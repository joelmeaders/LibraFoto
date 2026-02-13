using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.System;

/// <summary>
/// Get system information.
/// </summary>
public sealed class GetSystemInfoEndpoint : EndpointWithoutRequest<Ok<SystemInfoResponse>>
{
    public override void Configure()
    {
        Get("/api/admin/system/info");
        Tags("System");
        Summary(s =>
        {
            s.Summary = "Get system information";
            s.Description = "Returns current system information including version, uptime, and update status.";
        });
    }

    public override async Task<Ok<SystemInfoResponse>> ExecuteAsync(CancellationToken ct)
    {
        var systemService = Resolve<ISystemService>();
        return await HandleRequestAsync(systemService, ct);
    }

    internal static async Task<Ok<SystemInfoResponse>> HandleRequestAsync(
        ISystemService systemService,
        CancellationToken ct)
    {
        var info = await systemService.GetSystemInfoAsync(ct);
        return TypedResults.Ok(info);
    }
}
