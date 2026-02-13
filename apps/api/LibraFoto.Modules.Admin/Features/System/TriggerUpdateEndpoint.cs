using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.System;

/// <summary>
/// Trigger application update.
/// </summary>
public sealed class TriggerUpdateEndpoint : EndpointWithoutRequest<Accepted<UpdateTriggerResponse>>
{
    public override void Configure()
    {
        Post("/api/admin/system/update");
        Tags("System");
        Summary(s =>
        {
            s.Summary = "Trigger application update";
            s.Description = "Triggers the update process. The application will restart after updating.";
        });
    }

    public override async Task<Accepted<UpdateTriggerResponse>> ExecuteAsync(CancellationToken ct)
    {
        var systemService = Resolve<ISystemService>();
        return await HandleRequestAsync(systemService, ct);
    }

    internal static async Task<Accepted<UpdateTriggerResponse>> HandleRequestAsync(
        ISystemService systemService,
        CancellationToken ct)
    {
        var result = await systemService.TriggerUpdateAsync(ct);
        return TypedResults.Accepted((string?)null, result);
    }
}
