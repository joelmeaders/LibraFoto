using System.Security.Claims;
using FastEndpoints;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Authentication;

/// <summary>
/// Log out the current user by invalidating refresh tokens.
/// </summary>
public sealed class LogoutEndpoint : EndpointWithoutRequest<Results<Ok, UnauthorizedHttpResult>>
{
    public override void Configure()
    {
        Post("/api/auth/logout");
        Policy(p => p.RequireAuthenticatedUser());
        Tags("Authentication");
        Summary(s =>
        {
            s.Summary = "Log out the current user";
            s.Description = "Invalidates the current user's refresh tokens.";
        });
    }

    public override async Task<Results<Ok, UnauthorizedHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var authService = Resolve<IAuthService>();
        return await HandleRequestAsync(User, authService, ct);
    }

    internal static async Task<Results<Ok, UnauthorizedHttpResult>> HandleRequestAsync(
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        await authService.LogoutAsync(userId, cancellationToken);
        return TypedResults.Ok();
    }
}
