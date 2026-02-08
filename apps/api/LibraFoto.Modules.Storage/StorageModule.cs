using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Storage
{
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

        /// <summary>
        /// Maps Storage module endpoints to the application.
        /// </summary>
        public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder app)
        {
            // Map storage provider management endpoints
            app.MapStorageProviderEndpoints();

            // Map file upload endpoints
            app.MapUploadEndpoints();

            // Map Google Photos OAuth endpoints
            app.MapGooglePhotosOAuthEndpoints();

            // Map Google Photos Picker endpoints
            app.MapGooglePhotosPickerEndpoints();

            return app;
        }
    }
}
