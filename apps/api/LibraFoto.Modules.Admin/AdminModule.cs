using LibraFoto.Modules.Admin.Endpoints;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Admin;

/// <summary>
/// Admin module registration for management and configuration endpoints.
/// Handles photos, albums, tags, settings, and user management.
/// </summary>
public static class AdminModule
{
    /// <summary>
    /// Registers Admin module services with the DI container.
    /// </summary>
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        // Register infrastructure services
        services.AddMemoryCache();

        // Register module services
        services.AddScoped<IPhotoService, PhotoService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<ITagService, TagService>();
        services.AddSingleton<ISystemService, SystemService>();

        return services;
    }

    /// <summary>
    /// Maps Admin module endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");
        // .RequireAuthorization(); // Enable when auth is implemented

        // Map all admin sub-endpoints
        group.MapPhotoEndpoints();
        group.MapAlbumEndpoints();
        group.MapTagEndpoints();
        group.MapSystemEndpoints();

        return app;
    }
}
