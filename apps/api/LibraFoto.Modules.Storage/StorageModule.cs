using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Modules.Storage.Services.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Storage;

/// <summary>
/// Storage module registration for local and cloud storage providers.
/// Handles file storage, sync, and provider configuration.
/// </summary>
public static class StorageModule
{
    /// <summary>
    /// Registers Storage module services with the DI container.
    /// </summary>
    public static IServiceCollection AddStorageModule(this IServiceCollection services)
    {
        // Register media scanner (singleton for efficiency)
        services.AddSingleton<IMediaScannerService, MediaScannerService>();
        services.AddScoped<IStoragePersistenceRepository, StoragePersistenceRepository>();

        // Register storage provider factory (scoped to allow scoped dependencies)
        services.AddScoped<IStorageProviderFactory, StorageProviderFactory>();

        // Register sync service (scoped for per-request operations)
        services.AddScoped<ISyncService, SyncService>();

        // Register image import service for upload processing
        services.AddScoped<IImageImportService, ImageImportService>();

        // Register Google Photos Picker service
        services.AddScoped<GooglePhotosPickerService>();

        return services;
    }
}
