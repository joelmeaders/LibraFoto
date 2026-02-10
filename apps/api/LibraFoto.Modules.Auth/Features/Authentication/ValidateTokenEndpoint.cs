using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Authentication;

/// <summary>
/// Validate a JWT and return validity status.
/// </summary>
public sealed class ValidateTokenEndpoint : EndpointWithoutRequest<Ok<TokenValidationResult>>
{
    public override void Configure()
    {
        Post("/api/auth/validate");
        AllowAnonymous();
        Tags("Authentication");
        Summary(s =>
        {
            s.Summary = "Validate a token";
            s.Description = "Validates a JWT token and returns whether it's valid.";
        });
    }

    public override async Task<Ok<TokenValidationResult>> ExecuteAsync(CancellationToken ct)
    {
        var authService = Resolve<IAuthService>();
        var authorization = HttpContext.Request.Headers.Authorization.ToString();
        return await HandleRequestAsync(authorization, authService, ct);
    }

    internal static async Task<Ok<TokenValidationResult>> HandleRequestAsync(
        string? authorization,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer "))
        {
            return TypedResults.Ok(new TokenValidationResult(false, null));
        }

        var token = authorization.Substring("Bearer ".Length);
        var userId = await authService.ValidateTokenAsync(token, cancellationToken);

        return TypedResults.Ok(new TokenValidationResult(userId.HasValue, userId));
    }
}
