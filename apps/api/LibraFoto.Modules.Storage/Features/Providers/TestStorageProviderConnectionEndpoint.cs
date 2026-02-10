using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// Test connection to a storage provider.
/// </summary>
public sealed class TestStorageProviderConnectionEndpoint : Endpoint<TestStorageProviderConnectionRequest, Results<Ok<object>, NotFound<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";

    public override void Configure()
    {
        Post("/api/admin/storage/providers/{id:long}/test");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Test provider connection";
            s.Description = "Tests the connection to a storage provider.";
        });
    }

    public override async Task<Results<Ok<object>, NotFound<ApiError>>> ExecuteAsync(
        TestStorageProviderConnectionRequest req,
        CancellationToken ct)
    {
        var factory = Resolve<IStorageProviderFactory>();
        return await HandleRequestAsync(req.Id, factory, ct);
    }

    internal static async Task<Results<Ok<object>, NotFound<ApiError>>> HandleRequestAsync(
        long id,
        IStorageProviderFactory factory,
        CancellationToken cancellationToken)
    {
        var provider = await factory.GetProviderAsync(id, cancellationToken);

        if (provider == null)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {id} not found or disabled"));
        }

        var isConnected = await provider.TestConnectionAsync(cancellationToken);

        return TypedResults.Ok<object>(new
        {
            Connected = isConnected,
            Message = isConnected ? "Connection successful" : "Connection failed"
        });
    }
}

public sealed class TestStorageProviderConnectionRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
