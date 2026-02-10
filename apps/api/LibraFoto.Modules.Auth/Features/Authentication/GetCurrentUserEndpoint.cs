using System.Security.Claims;
using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Authentication;

/// <summary>
/// Get the current authenticated user's profile information.
/// </summary>
public sealed class GetCurrentUserEndpoint : EndpointWithoutRequest<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>>
{
    public override void Configure()
    {
        Get("/api/auth/me");
        Policy(p => p.RequireAuthenticatedUser());
        Tags("Authentication");
        Summary(s =>
        {
            s.Summary = "Get current user information";
            s.Description = "Returns the authenticated user's profile information.";
        });
    }

    public override async Task<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var authService = Resolve<IAuthService>();
        return await HandleRequestAsync(User, authService, ct);
    }

    internal static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>> HandleRequestAsync(
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var currentUser = await authService.GetCurrentUserAsync(userId, cancellationToken);

        if (currentUser == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(currentUser);
    }
}
