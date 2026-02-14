using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services.Repositories;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// Create a new storage provider.
/// </summary>
public sealed class CreateStorageProviderEndpoint : Endpoint<CreateStorageProviderRequest, Results<Created<StorageProviderDto>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/admin/storage/providers");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Create a new storage provider";
            s.Description = "Creates and configures a new storage provider.";
        });
    }

    public override async Task<Results<Created<StorageProviderDto>, BadRequest<ApiError>>> ExecuteAsync(
        CreateStorageProviderRequest req,
        CancellationToken ct)
    {
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var factory = Resolve<IStorageProviderFactory>();
        return await HandleRequestAsync(req, storageRepository, factory, ct);
    }

    internal static async Task<Results<Created<StorageProviderDto>, BadRequest<ApiError>>> HandleRequestAsync(
        CreateStorageProviderRequest request,
        IStoragePersistenceRepository storageRepository,
        IStorageProviderFactory factory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest(new ApiError("VALIDATION_ERROR", "Name is required"));
        }

        try
        {
            var testProvider = factory.CreateProvider(request.Type);
            testProvider.Initialize(0, request.Name, request.Configuration);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new ApiError("INVALID_CONFIGURATION", $"Invalid configuration: {ex.Message}"));
        }

        var entity = new Data.Entities.StorageProvider
        {
            Type = request.Type,
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            Configuration = request.Configuration
        };

        await storageRepository.AddProviderAsync(entity, cancellationToken);
        await storageRepository.SaveChangesAsync(cancellationToken);

        factory.ClearCache();

        var dto = new StorageProviderDto
        {
            Id = entity.Id,
            Type = entity.Type,
            Name = entity.Name,
            IsEnabled = entity.IsEnabled,
            SupportsUpload = entity.Type == StorageProviderType.Local,
            SupportsWatch = entity.Type == StorageProviderType.Local,
            PhotoCount = 0,
            IsConnected = entity.Type == StorageProviderType.Local ? null : false
        };

        return TypedResults.Created($"/api/admin/storage/providers/{entity.Id}", dto);
    }
}
