using LibraFoto.Modules.Auth.Endpoints;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Modules.Auth;

/// <summary>
/// Auth module registration for authentication and authorization.
/// Handles user management, login, roles, and guest access.
/// </summary>
public static class AuthModule
{
    /// <summary>
    /// Registers Auth module services with the DI container.
    /// </summary>
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        // Register auth services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IGuestLinkService, GuestLinkService>();

        // Add authorization services
        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Maps Auth module endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Map all authentication-related endpoints
        app.MapAuthenticationEndpoints();
        app.MapSetupEndpoints();
        app.MapUserEndpoints();
        app.MapGuestLinkEndpoints();

        return app;
    }
}
