using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Sync;

/// <summary>
/// Get sync status for a provider.
/// </summary>
public sealed class GetSyncStatusEndpoint : Endpoint<GetSyncStatusRequest, Ok<SyncStatus>>
{
    public override void Configure()
    {
        Get("/api/admin/storage/sync/{id:long}/status");
        Tags("Storage Sync");
        Summary(s =>
        {
            s.Summary = "Get sync status";
            s.Description = "Returns the current sync status for a storage provider.";
        });
    }

    public override async Task<Ok<SyncStatus>> ExecuteAsync(GetSyncStatusRequest req, CancellationToken ct)
    {
        var syncService = Resolve<ISyncService>();
        return await HandleRequestAsync(req.Id, syncService, ct);
    }

    internal static async Task<Ok<SyncStatus>> HandleRequestAsync(
        long id,
        ISyncService syncService,
        CancellationToken cancellationToken)
    {
        var status = await syncService.GetSyncStatusAsync(id, cancellationToken);
        return TypedResults.Ok(status);
    }
}

public sealed class GetSyncStatusRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
