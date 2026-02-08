using LibraFoto.Modules.Display.Endpoints;
using LibraFoto.Modules.Display.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Display
{
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
            // Register display settings service (scoped for per-request database context)
            services.AddScoped<IDisplaySettingsService, DisplaySettingsService>();

            // Register slideshow service (scoped to use the same DbContext per request)
            services.AddScoped<ISlideshowService, SlideshowService>();

            return services;
        }

        /// <summary>
        /// Maps Display module endpoints to the application.
        /// </summary>
        public static IEndpointRouteBuilder MapDisplayEndpoints(this IEndpointRouteBuilder app)
        {
            // Map slideshow endpoints (/api/display/photos/*)
            app.MapSlideshowEndpoints();

            // Map display settings endpoints (/api/display/settings/*)
            app.MapDisplaySettingsEndpoints();

            // Map display config endpoints (/api/display/config/*)
            app.MapDisplayConfigEndpoints();

            return app;
        }
    }
}
