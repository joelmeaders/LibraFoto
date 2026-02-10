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
        // Register auth services (singleton for AuthService to maintain token state, scoped for others)
        services.AddSingleton<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IGuestLinkService, GuestLinkService>();

        // Add authorization services
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
        });

        return services;
    }
}
