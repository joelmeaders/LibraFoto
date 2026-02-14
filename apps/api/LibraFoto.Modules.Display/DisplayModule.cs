using LibraFoto.Modules.Display.Services;
using LibraFoto.Modules.Display.Services.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Display;

/// <summary>
/// Display module registration for the digital picture frame endpoints.
/// Handles photo/video streaming and slideshow configuration.
/// </summary>
public static class DisplayModule
{
    /// <summary>
    /// Registers Display module services with the DI container.
    /// </summary>
    public static IServiceCollection AddDisplayModule(this IServiceCollection services)
    {
        services.AddScoped<IDisplaySettingsRepository, DisplaySettingsRepository>();
        services.AddScoped<ISlideshowRepository, SlideshowRepository>();

        // Register display settings service (scoped for per-request database context)
        services.AddScoped<IDisplaySettingsService, DisplaySettingsService>();

        // Register slideshow service as singleton (maintains state across requests, creates scoped DbContext internally)
        services.AddSingleton<ISlideshowService, SlideshowService>();

        return services;
    }
}
