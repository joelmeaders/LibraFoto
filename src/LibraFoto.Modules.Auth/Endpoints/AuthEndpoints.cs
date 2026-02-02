using System.Security.Claims;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Auth.Endpoints;

/// <summary>
/// Authentication endpoints for login, logout, and token management.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate a user")
            .WithDescription("Authenticates a user with email and password, returning JWT tokens.")
            .AllowAnonymous();

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Log out the current user")
            .WithDescription("Invalidates the current user's refresh tokens.")
            .RequireAuthorization();

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Get current user information")
            .WithDescription("Returns the authenticated user's profile information.")
            .RequireAuthorization();

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token")
            .WithDescription("Exchanges a refresh token for a new access token.")
            .AllowAnonymous();

        group.MapPost("/validate", ValidateToken)
            .WithName("ValidateToken")
            .WithSummary("Validate a token")
            .WithDescription("Validates a JWT token and returns whether it's valid.")
            .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Authenticates a user and returns tokens.
    /// </summary>
    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> Login(
        [FromBody] LoginRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            var errors = new Dictionary<string, string[]>
            {
                { "credentials", new[] { "Email and password are required." } }
            };
            return TypedResults.ValidationProblem(errors);
        }

        var result = await authService.LoginAsync(request, cancellationToken);

        if (result == null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    private static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(
        ClaimsPrincipal user,
        [FromServices] IAuthService authService,
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

    /// <summary>
    /// Gets the current authenticated user's information.
    /// </summary>
    private static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>> GetCurrentUser(
        ClaimsPrincipal user,
        [FromServices] IAuthService authService,
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

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var errors = new Dictionary<string, string[]>
            {
                { "refreshToken", new[] { "Refresh token is required." } }
            };
            return TypedResults.ValidationProblem(errors);
        }

        var result = await authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (result == null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Validates a JWT token.
    /// </summary>
    private static async Task<Ok<Models.TokenValidationResult>> ValidateToken(
        [FromHeader(Name = "Authorization")] string? authorization,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer "))
        {
            return TypedResults.Ok(new Models.TokenValidationResult(false, null));
        }

        var token = authorization.Substring("Bearer ".Length);
        var userId = await authService.ValidateTokenAsync(token, cancellationToken);

        return TypedResults.Ok(new Models.TokenValidationResult(userId.HasValue, userId));
    }
}
