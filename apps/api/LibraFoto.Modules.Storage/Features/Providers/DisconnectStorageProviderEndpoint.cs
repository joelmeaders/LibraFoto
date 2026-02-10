using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// Disconnect a storage provider.
/// </summary>
public sealed class DisconnectStorageProviderEndpoint : Endpoint<DisconnectStorageProviderRequest, Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string LoggerCategory = "StorageEndpoints";

    public override void Configure()
    {
        Post("/api/admin/storage/providers/{id:long}/disconnect");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Disconnect a storage provider";
            s.Description = "Clears OAuth tokens and disables a provider without deleting it.";
        });
    }

    public override async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        DisconnectStorageProviderRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var factory = Resolve<IStorageProviderFactory>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.Id, dbContext, factory, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long id,
        LibraFotoDbContext dbContext,
        IStorageProviderFactory factory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var entity = await dbContext.StorageProviders.FindAsync([id], cancellationToken);

        if (entity == null)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {id} not found"));
        }

        var provider = await factory.GetProviderAsync(id, cancellationToken);
        if (provider is not IOAuthProvider oauthProvider)
        {
            return TypedResults.BadRequest(new ApiError("PROVIDER_NOT_OAUTH", "Provider does not support OAuth disconnect"));
        }

        var disconnected = await oauthProvider.DisconnectAsync(entity, cancellationToken);
        if (!disconnected)
        {
            logger.LogWarning("OAuth disconnect reported failure for provider {ProviderId}", id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        factory.ClearCache();

        var photoCount = await dbContext.Photos.CountAsync(p => p.ProviderId == id, cancellationToken);

        var dto = new StorageProviderDto
        {
            Id = entity.Id,
            Type = entity.Type,
            Name = entity.Name,
            IsEnabled = entity.IsEnabled,
            SupportsUpload = entity.Type == StorageProviderType.Local,
            SupportsWatch = entity.Type == StorageProviderType.Local,
            LastSyncDate = entity.LastSyncDate,
            PhotoCount = photoCount,
            IsConnected = false,
            StatusMessage = "Disconnected"
        };

        return TypedResults.Ok(dto);
    }
}

public sealed class DisconnectStorageProviderRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
