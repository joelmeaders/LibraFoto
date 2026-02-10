using LibraFoto.Modules.Admin.Services;
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
}
