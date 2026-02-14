using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Repositories;
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
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IAlbumRepository, AlbumRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IPhotoService, PhotoService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<ITagService, TagService>();
        services.AddSingleton<ISystemService, SystemService>();

        return services;
    }
}
