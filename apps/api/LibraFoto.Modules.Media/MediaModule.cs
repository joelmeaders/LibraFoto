using LibraFoto.Modules.Media.Endpoints;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Media;

/// <summary>
/// Media module registration for image and video processing.
/// Handles thumbnails, optimization, metadata extraction, and format conversion.
/// </summary>
public static class MediaModule
{
    /// <summary>
    /// Registers Media module services with the DI container.
    /// </summary>
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        // Register ThumbnailService with proper storage path configuration
        services.AddScoped<IThumbnailService>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
            var thumbnailPath = Path.Combine(storagePath, ".thumbnails");
            return new ThumbnailService(thumbnailPath);
        });

        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IImageProcessor, ImageProcessor>();

        // Register HttpClient for geocoding service with proper configuration
        services.AddHttpClient<IGeocodingService, GeocodingService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.Add("User-Agent", "LibraFoto/1.0 (Digital Picture Frame)");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// Maps Media module endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPhotoEndpoints();
        app.MapThumbnailEndpoints();
        app.MapMetadataEndpoints();

        return app;
    }
}
