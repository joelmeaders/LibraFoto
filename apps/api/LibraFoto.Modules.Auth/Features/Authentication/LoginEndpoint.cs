using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Authentication;

/// <summary>
/// Authenticate a user and return JWT tokens.
/// </summary>
public sealed class LoginEndpoint : Endpoint<LoginRequest, Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>>
{
    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
        Tags("Authentication");
        Summary(s =>
        {
            s.Summary = "Authenticate a user";
            s.Description = "Authenticates a user with email and password, returning JWT tokens.";
        });
    }

    public override async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> ExecuteAsync(
        LoginRequest req,
        CancellationToken ct)
    {
        var authService = Resolve<IAuthService>();
        return await HandleRequestAsync(req, authService, ct);
    }

    internal static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> HandleRequestAsync(
        LoginRequest request,
        IAuthService authService,
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
}
