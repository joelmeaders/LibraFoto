using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Sync;

/// <summary>
/// Trigger a sync for all providers.
/// </summary>
public sealed class TriggerSyncAllEndpoint : Endpoint<TriggerSyncAllRequest, Ok<SyncResult[]>>
{
    public override void Configure()
    {
        Post("/api/admin/storage/sync/all");
        Tags("Storage Sync");
        Summary(s =>
        {
            s.Summary = "Trigger sync for all providers";
            s.Description = "Starts a sync operation for all enabled storage providers.";
        });
    }

    public override async Task<Ok<SyncResult[]>> ExecuteAsync(TriggerSyncAllRequest req, CancellationToken ct)
    {
        var syncService = Resolve<ISyncService>();
        return await HandleRequestAsync(req, syncService, ct);
    }

    internal static async Task<Ok<SyncResult[]>> HandleRequestAsync(
        TriggerSyncAllRequest request,
        ISyncService syncService,
        CancellationToken cancellationToken)
    {
        var syncRequest = request.ToSyncRequest();
        var results = await syncService.SyncAllProvidersAsync(syncRequest, cancellationToken);
        return TypedResults.Ok(results.ToArray());
    }
}

public sealed class TriggerSyncAllRequest
{
    public bool FullSync { get; init; } = false;

    public bool RemoveDeleted { get; init; } = true;

    public bool SkipExisting { get; init; } = true;

    public int MaxFiles { get; init; } = 0;

    public string? FolderId { get; init; }

    public bool Recursive { get; init; } = true;

    internal SyncRequest ToSyncRequest() => new()
    {
        FullSync = FullSync,
        RemoveDeleted = RemoveDeleted,
        SkipExisting = SkipExisting,
        MaxFiles = MaxFiles,
        FolderId = FolderId,
        Recursive = Recursive
    };
}
