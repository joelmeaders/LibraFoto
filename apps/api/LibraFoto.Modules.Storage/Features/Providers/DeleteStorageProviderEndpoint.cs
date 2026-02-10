using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Providers;

/// <summary>
/// Delete a storage provider.
/// </summary>
public sealed class DeleteStorageProviderEndpoint : Endpoint<DeleteProviderRequest, Results<NoContent, NotFound<ApiError>>>
{
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string LoggerCategory = "StorageEndpoints";

    public override void Configure()
    {
        Delete("/api/admin/storage/providers/{id:long}");
        Tags("Storage Providers");
        Summary(s =>
        {
            s.Summary = "Delete a storage provider";
            s.Description = "Deletes a storage provider and optionally its photos.";
        });
    }

    public override async Task<Results<NoContent, NotFound<ApiError>>> ExecuteAsync(
        DeleteProviderRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var factory = Resolve<IStorageProviderFactory>();
        var configuration = Resolve<IConfiguration>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req, dbContext, factory, configuration, loggerFactory, ct);
    }

    internal static async Task<Results<NoContent, NotFound<ApiError>>> HandleRequestAsync(
        DeleteProviderRequest request,
        LibraFotoDbContext dbContext,
        IStorageProviderFactory factory,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var entity = await dbContext.StorageProviders.FindAsync([request.Id], cancellationToken);

        if (entity == null)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {request.Id} not found"));
        }

        await DisconnectOAuthProviderAsync(entity, factory, logger, cancellationToken);
        await HandleProviderPhotosAsync(request, dbContext, configuration, logger, cancellationToken);

        dbContext.StorageProviders.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        factory.ClearCache();

        logger.LogInformation("Deleted storage provider {ProviderId}", request.Id);
        return TypedResults.NoContent();
    }

    private static async Task DisconnectOAuthProviderAsync(
        Data.Entities.StorageProvider entity,
        IStorageProviderFactory factory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = await factory.GetProviderAsync(entity.Id, cancellationToken);
            if (provider is IOAuthProvider oauthProvider)
            {
                var disconnected = await oauthProvider.DisconnectAsync(entity, cancellationToken);
                if (!disconnected)
                {
                    logger.LogWarning("Failed to disconnect OAuth provider {ProviderId}, continuing with deletion", entity.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Exception during OAuth disconnect for provider {ProviderId}, continuing with deletion", entity.Id);
        }
    }

    private static async Task HandleProviderPhotosAsync(
        DeleteProviderRequest request,
        LibraFotoDbContext dbContext,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var photos = await dbContext.Photos.Where(p => p.ProviderId == request.Id).ToListAsync(cancellationToken);

        if (request.DeletePhotos)
        {
            await DeleteProviderPhotoThumbnailsAsync(photos, configuration, logger);
            dbContext.Photos.RemoveRange(photos);
            logger.LogInformation("Deleted {Count} photos from provider {ProviderId}", photos.Count, request.Id);
            return;
        }

        foreach (var photo in photos)
        {
            photo.ProviderId = null;
            photo.ProviderFileId = null;
        }

        logger.LogInformation("Unlinked {Count} photos from provider {ProviderId}", photos.Count, request.Id);
    }

    private static Task DeleteProviderPhotoThumbnailsAsync(
        IEnumerable<Data.Entities.Photo> photos,
        IConfiguration configuration,
        ILogger logger)
    {
        var storagePath = configuration["Storage:LocalPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "photos");

        foreach (var photo in photos.Where(photo => !string.IsNullOrEmpty(photo.ThumbnailPath)))
        {
            try
            {
                var thumbnailFullPath = Path.Combine(storagePath, photo.ThumbnailPath!);
                if (File.Exists(thumbnailFullPath))
                {
                    File.Delete(thumbnailFullPath);
                    logger.LogDebug("Deleted thumbnail for photo {PhotoId}", photo.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete thumbnail for photo {PhotoId}, continuing", photo.Id);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class DeleteProviderRequest
{
    public DeleteProviderRequest()
    {
    }

    public DeleteProviderRequest(long id, bool deletePhotos)
    {
        Id = id;
        DeletePhotos = deletePhotos;
    }

    [BindFrom("id")]
    public long Id { get; init; }

    [QueryParam]
    public bool DeletePhotos { get; init; }
}
