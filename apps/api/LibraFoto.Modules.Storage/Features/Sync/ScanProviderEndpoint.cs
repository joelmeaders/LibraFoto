using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Sync;

/// <summary>
/// Scan a provider for new files without importing.
/// </summary>
public sealed class ScanProviderEndpoint : Endpoint<ScanProviderRequest, Results<Ok<ScanResult>, NotFound<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";

    public override void Configure()
    {
        Get("/api/admin/storage/sync/{id:long}/scan");
        Tags("Storage Sync");
        Summary(s =>
        {
            s.Summary = "Scan provider for new files";
            s.Description = "Scans the provider for new files without importing them.";
        });
    }

    public override async Task<Results<Ok<ScanResult>, NotFound<ApiError>>> ExecuteAsync(ScanProviderRequest req, CancellationToken ct)
    {
        var syncService = Resolve<ISyncService>();
        return await HandleRequestAsync(req.Id, syncService, ct);
    }

    internal static async Task<Results<Ok<ScanResult>, NotFound<ApiError>>> HandleRequestAsync(
        long id,
        ISyncService syncService,
        CancellationToken cancellationToken)
    {
        var result = await syncService.ScanProviderAsync(id, cancellationToken);

        if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, result.ErrorMessage));
        }

        return TypedResults.Ok(result);
    }
}

public sealed class ScanProviderRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
