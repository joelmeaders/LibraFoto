using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Sync;

/// <summary>
/// Cancel an in-progress sync.
/// </summary>
public sealed class CancelSyncEndpoint : Endpoint<CancelSyncRequest, Ok<object>>
{
    public override void Configure()
    {
        Post("/api/admin/storage/sync/{id:long}/cancel");
        Tags("Storage Sync");
        Summary(s =>
        {
            s.Summary = "Cancel sync operation";
            s.Description = "Cancels an in-progress sync operation.";
        });
    }

    public override Task<Ok<object>> ExecuteAsync(CancelSyncRequest req, CancellationToken ct)
    {
        var syncService = Resolve<ISyncService>();
        return Task.FromResult(HandleRequest(req.Id, syncService));
    }

    internal static Ok<object> HandleRequest(long id, ISyncService syncService)
    {
        var cancelled = syncService.CancelSync(id);
        return TypedResults.Ok<object>(new { Cancelled = cancelled });
    }
}

public sealed class CancelSyncRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
