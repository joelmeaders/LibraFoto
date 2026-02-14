using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Authentication;

/// <summary>
/// Exchange refresh token for a new access token.
/// </summary>
public sealed class RefreshTokenEndpoint : Endpoint<RefreshTokenRequest, Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>>
{
    public override void Configure()
    {
        Post("/api/auth/refresh");
        AllowAnonymous();
        Tags("Authentication");
        Summary(s =>
        {
            s.Summary = "Refresh access token";
            s.Description = "Exchanges a refresh token for a new access token.";
        });
    }

    public override async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> ExecuteAsync(
        RefreshTokenRequest req,
        CancellationToken ct)
    {
        var authService = Resolve<IAuthService>();
        return await HandleRequestAsync(req, authService, ct);
    }

    internal static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> HandleRequestAsync(
        RefreshTokenRequest request,
        IAuthService authService,
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
}

public record RefreshTokenRequest(
    [Required]
    string RefreshToken
);
