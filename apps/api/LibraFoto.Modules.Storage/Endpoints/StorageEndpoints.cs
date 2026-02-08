using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Endpoints
{
    /// <summary>
    /// Endpoints for storage provider management.
    /// </summary>
    public static class StorageEndpoints
    {
        private const string ProviderIdRoute = "/{id:long}";
        private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
        private const string StorageEndpointsLogger = "StorageEndpoints";
        private static readonly string[] _googlePhotosRequiredScopes =
        [
            "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
        ];

        /// <summary>
        /// Maps storage provider management endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapStorageProviderEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/admin/storage/providers")
                .WithTags("Storage Providers");

            group.MapGet("/", GetAllProviders)
                .WithName("GetStorageProviders")
                .WithSummary("Get all storage providers")
                .WithDescription("Returns a list of all configured storage providers.");

            group.MapGet(ProviderIdRoute, GetProvider)
                .WithName("GetStorageProvider")
                .WithSummary("Get a storage provider by ID")
                .WithDescription("Returns details of a specific storage provider.");

            group.MapPost("/", CreateProvider)
                .WithName("CreateStorageProvider")
                .WithSummary("Create a new storage provider")
                .WithDescription("Creates and configures a new storage provider.");

            group.MapPut(ProviderIdRoute, UpdateProvider)
                .WithName("UpdateStorageProvider")
                .WithSummary("Update a storage provider")
                .WithDescription("Updates an existing storage provider configuration.");

            group.MapDelete(ProviderIdRoute, DeleteProvider)
                .WithName("DeleteStorageProvider")
                .WithSummary("Delete a storage provider")
                .WithDescription("Deletes a storage provider and optionally its photos.");

            group.MapPost($"{ProviderIdRoute}/disconnect", DisconnectProvider)
                .WithName("DisconnectStorageProvider")
                .WithSummary("Disconnect a storage provider")
                .WithDescription("Clears OAuth tokens and disables a provider without deleting it.");

            group.MapPost($"{ProviderIdRoute}/test", TestProviderConnection)
                .WithName("TestStorageProviderConnection")
                .WithSummary("Test provider connection")
                .WithDescription("Tests the connection to a storage provider.");

            // Sync endpoints
            var syncGroup = app.MapGroup("/api/admin/storage/sync")
                .WithTags("Storage Sync");

            syncGroup.MapPost(ProviderIdRoute, TriggerSync)
                .WithName("TriggerSync")
                .WithSummary("Trigger a sync for a provider")
                .WithDescription("Starts a sync operation for the specified storage provider.");

            syncGroup.MapPost("/all", TriggerSyncAll)
                .WithName("TriggerSyncAll")
                .WithSummary("Trigger sync for all providers")
                .WithDescription("Starts a sync operation for all enabled storage providers.");

            syncGroup.MapGet($"{ProviderIdRoute}/status", GetSyncStatus)
                .WithName("GetSyncStatus")
                .WithSummary("Get sync status")
                .WithDescription("Returns the current sync status for a storage provider.");

            syncGroup.MapPost($"{ProviderIdRoute}/cancel", CancelSync)
                .WithName("CancelSync")
                .WithSummary("Cancel sync operation")
                .WithDescription("Cancels an in-progress sync operation.");

            syncGroup.MapGet($"{ProviderIdRoute}/scan", ScanProvider)
                .WithName("ScanProvider")
                .WithSummary("Scan provider for new files")
                .WithDescription("Scans the provider for new files without importing them.");

            return app;
        }

        /// <summary>
        /// Gets all storage providers.
        /// </summary>
        private static async Task<Ok<StorageProviderDto[]>> GetAllProviders(
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            CancellationToken cancellationToken)
        {
            var entities = await dbContext.StorageProviders
                .AsNoTracking()
                .ToArrayAsync(cancellationToken);

            var providers = new List<StorageProviderDto>();

            foreach (var entity in entities)
            {
                bool? isConnected = null;

                // Check connection status for cloud providers
                if (entity.Type != StorageProviderType.Local)
                {
                    try
                    {
                        var provider = await factory.GetProviderAsync(entity.Id, cancellationToken);
                        if (provider != null)
                        {
                            isConnected = await provider.TestConnectionAsync(cancellationToken);
                        }
                    }
                    catch
                    {
                        isConnected = false;
                    }
                }

                var photoCount = await dbContext.Photos.CountAsync(p => p.ProviderId == entity.Id, cancellationToken);

                providers.Add(new StorageProviderDto
                {
                    Id = entity.Id,
                    Type = entity.Type,
                    Name = entity.Name,
                    IsEnabled = entity.IsEnabled,
                    SupportsUpload = entity.Type == StorageProviderType.Local,
                    SupportsWatch = entity.Type == StorageProviderType.Local,
                    LastSyncDate = entity.LastSyncDate,
                    PhotoCount = photoCount,
                    IsConnected = isConnected,
                    StatusMessage = GetProviderStatusMessage(entity)
                });
            }

            return TypedResults.Ok(providers.ToArray());
        }

        /// <summary>
        /// Gets a specific storage provider.
        /// </summary>
        private static async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>>> GetProvider(
            long id,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            CancellationToken cancellationToken)
        {
            var entity = await dbContext.StorageProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (entity == null)
            {
                return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {id} not found"));
            }

            bool? isConnected = null;

            // Check connection status for cloud providers
            if (entity.Type != StorageProviderType.Local)
            {
                try
                {
                    var provider = await factory.GetProviderAsync(id, cancellationToken);
                    if (provider != null)
                    {
                        isConnected = await provider.TestConnectionAsync(cancellationToken);
                    }
                }
                catch
                {
                    isConnected = false;
                }
            }

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
                IsConnected = isConnected,
                StatusMessage = GetProviderStatusMessage(entity)
            };

            return TypedResults.Ok(dto);
        }

        /// <summary>
        /// Creates a new storage provider.
        /// </summary>
        private static async Task<Results<Created<StorageProviderDto>, BadRequest<ApiError>>> CreateProvider(
            [FromBody] CreateStorageProviderRequest request,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return TypedResults.BadRequest(new ApiError("VALIDATION_ERROR", "Name is required"));
            }

            // Validate configuration by creating a provider instance
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

            dbContext.StorageProviders.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

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
                IsConnected = entity.Type == StorageProviderType.Local ? null : false // Cloud providers start disconnected
            };

            return TypedResults.Created($"/api/admin/storage/providers/{entity.Id}", dto);
        }

        /// <summary>
        /// Updates an existing storage provider.
        /// </summary>
        private static async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>> UpdateProvider(
            long id,
            [FromBody] UpdateStorageProviderRequest request,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            CancellationToken cancellationToken)
        {
            var entity = await dbContext.StorageProviders.FindAsync([id], cancellationToken);

            if (entity == null)
            {
                return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {id} not found"));
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                entity.Name = request.Name;
            }

            if (request.Configuration != null)
            {
                // Validate configuration
                try
                {
                    var testProvider = factory.CreateProvider(entity.Type);
                    testProvider.Initialize(0, entity.Name, request.Configuration);
                }
                catch (Exception ex)
                {
                    return TypedResults.BadRequest(new ApiError("INVALID_CONFIGURATION", $"Invalid configuration: {ex.Message}"));
                }

                entity.Configuration = request.Configuration;
            }

            if (request.IsEnabled.HasValue)
            {
                entity.IsEnabled = request.IsEnabled.Value;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            factory.ClearCache();

            var photoCount = await dbContext.Photos.CountAsync(p => p.ProviderId == id, cancellationToken);

            bool? isConnected = null;

            // Check connection status for cloud providers
            if (entity.Type != StorageProviderType.Local)
            {
                try
                {
                    var provider = await factory.GetProviderAsync(id, cancellationToken);
                    if (provider != null)
                    {
                        isConnected = await provider.TestConnectionAsync(cancellationToken);
                    }
                }
                catch
                {
                    isConnected = false;
                }
            }

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
                IsConnected = isConnected,
                StatusMessage = GetProviderStatusMessage(entity)
            };

            return TypedResults.Ok(dto);
        }

        /// <summary>
        /// Deletes a storage provider.
        /// </summary>
        private static async Task<Results<NoContent, NotFound<ApiError>>> DeleteProvider(
            [AsParameters] DeleteProviderRequest request,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            [FromServices] IConfiguration configuration,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var logger = loggerFactory.CreateLogger(StorageEndpointsLogger);
            var entity = await dbContext.StorageProviders.FindAsync([request.Id], cancellationToken);

            if (entity == null)
            {
                return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, $"Storage provider with ID {request.Id} not found"));
            }

            await DisconnectOAuthProviderAsync(entity, factory, logger, cancellationToken);
            await HandleProviderPhotosAsync(request, dbContext, configuration, logger, cancellationToken);

            // Step 4: Remove the storage provider (required step)
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

        /// <summary>
        /// Disconnects a storage provider without deleting it.
        /// </summary>
        private static async Task<Results<Ok<StorageProviderDto>, NotFound<ApiError>, BadRequest<ApiError>>> DisconnectProvider(
            long id,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IStorageProviderFactory factory,
            [FromServices] ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var logger = loggerFactory.CreateLogger(StorageEndpointsLogger);
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

        /// <summary>
        /// Tests connection to a storage provider.
        /// </summary>
        private static async Task<Results<Ok<object>, NotFound<ApiError>>> TestProviderConnection(
            long id,
            [FromServices] IStorageProviderFactory factory,
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

        /// <summary>
        /// Triggers a sync for a storage provider.
        /// </summary>
        private static async Task<Results<Ok<SyncResult>, NotFound<ApiError>>> TriggerSync(
            long id,
            [FromBody] SyncRequest? request,
            [FromServices] ISyncService syncService,
            CancellationToken cancellationToken)
        {
            var result = await syncService.SyncProviderAsync(id, request ?? new SyncRequest(), cancellationToken);

            if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
            {
                return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, result.ErrorMessage));
            }

            return TypedResults.Ok(result);
        }

        /// <summary>
        /// Triggers a sync for all storage providers.
        /// </summary>
        private static async Task<Ok<SyncResult[]>> TriggerSyncAll(
            [FromBody] SyncRequest? request,
            [FromServices] ISyncService syncService,
            CancellationToken cancellationToken)
        {
            var results = await syncService.SyncAllProvidersAsync(request ?? new SyncRequest(), cancellationToken);
            return TypedResults.Ok(results.ToArray());
        }

        /// <summary>
        /// Gets the sync status for a provider.
        /// </summary>
        private static async Task<Ok<SyncStatus>> GetSyncStatus(
            long id,
            [FromServices] ISyncService syncService,
            CancellationToken cancellationToken)
        {
            var status = await syncService.GetSyncStatusAsync(id, cancellationToken);
            return TypedResults.Ok(status);
        }

        /// <summary>
        /// Cancels an in-progress sync.
        /// </summary>
        private static Ok<object> CancelSync(
            long id,
            [FromServices] ISyncService syncService)
        {
            var cancelled = syncService.CancelSync(id);
            return TypedResults.Ok<object>(new { Cancelled = cancelled });
        }

        /// <summary>
        /// Scans a provider for new files.
        /// </summary>
        private static async Task<Results<Ok<ScanResult>, NotFound<ApiError>>> ScanProvider(
            long id,
            [FromServices] ISyncService syncService,
            CancellationToken cancellationToken)
        {
            var result = await syncService.ScanProviderAsync(id, cancellationToken);

            if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
            {
                return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, result.ErrorMessage));
            }

            return TypedResults.Ok(result);
        }

        private static string? GetProviderStatusMessage(Data.Entities.StorageProvider entity)
        {
            if (entity.Type != StorageProviderType.GooglePhotos || string.IsNullOrWhiteSpace(entity.Configuration))
            {
                return null;
            }

            try
            {
                var config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(entity.Configuration);
                if (config?.GrantedScopes is { Length: > 0 } && !HasRequiredScopes(config.GrantedScopes))
                {
                    var missingScopes = _googlePhotosRequiredScopes
                        .Where(scope => !config.GrantedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
                        .Select(FormatScope)
                        .ToArray();

                    return missingScopes.Length == 0
                        ? "Reconnect required: missing Google Photos permissions."
                        : $"Reconnect required: missing Google Photos permissions ({string.Join(", ", missingScopes)}).";
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        private static bool HasRequiredScopes(IEnumerable<string> grantedScopes)
        {
            var scopeSet = new HashSet<string>(grantedScopes, StringComparer.OrdinalIgnoreCase);
            return _googlePhotosRequiredScopes.All(scopeSet.Contains);
        }

        private static string FormatScope(string scope)
        {
            var lastSlash = scope.LastIndexOf('/');
            return lastSlash >= 0 ? scope[(lastSlash + 1)..] : scope;
        }
    }

    public record DeleteProviderRequest(
        long Id,
        [FromQuery] bool DeletePhotos);
}
