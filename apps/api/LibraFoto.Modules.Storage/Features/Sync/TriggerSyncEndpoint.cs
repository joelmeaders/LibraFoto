using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Sync;

/// <summary>
/// Trigger a sync for a storage provider.
/// </summary>
public sealed class TriggerSyncEndpoint : Endpoint<TriggerSyncRequest, Results<Ok<SyncResult>, NotFound<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";

    public override void Configure()
    {
        Post("/api/admin/storage/sync/{id:long}");
        Tags("Storage Sync");
        Summary(s =>
        {
            s.Summary = "Trigger a sync for a provider";
            s.Description = "Starts a sync operation for the specified storage provider.";
        });
    }

    public override async Task<Results<Ok<SyncResult>, NotFound<ApiError>>> ExecuteAsync(
        TriggerSyncRequest req,
        CancellationToken ct)
    {
        var syncService = Resolve<ISyncService>();
        return await HandleRequestAsync(req, syncService, ct);
    }

    internal static async Task<Results<Ok<SyncResult>, NotFound<ApiError>>> HandleRequestAsync(
        TriggerSyncRequest request,
        ISyncService syncService,
        CancellationToken cancellationToken)
    {
        var syncRequest = request.ToSyncRequest();
        var result = await syncService.SyncProviderAsync(request.Id, syncRequest, cancellationToken);

        if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, result.ErrorMessage));
        }

        return TypedResults.Ok(result);
    }
}

public sealed class TriggerSyncRequest
{
    [BindFrom("id")]
    public long Id { get; init; }

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
