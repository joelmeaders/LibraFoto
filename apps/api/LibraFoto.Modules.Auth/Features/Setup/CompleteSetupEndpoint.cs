using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Setup;

/// <summary>
/// Complete initial setup by creating the first admin user.
/// </summary>
public sealed class CompleteSetupEndpoint : Endpoint<SetupRequest, Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>>
{
    public override void Configure()
    {
        Post("/api/setup/complete");
        AllowAnonymous();
        Tags("Setup");
        Summary(s =>
        {
            s.Summary = "Complete initial setup";
            s.Description = "Creates the first admin user. Only works if no users exist.";
        });
    }

    public override async Task<Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>> ExecuteAsync(
        SetupRequest req,
        CancellationToken ct)
    {
        var setupService = Resolve<ISetupService>();
        return await HandleRequestAsync(req, setupService, ct);
    }

    internal static async Task<Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>> HandleRequestAsync(
        SetupRequest request,
        ISetupService setupService,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = new[] { "Email is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Password is required." };
        }
        else if (request.Password.Length < 6)
        {
            errors["password"] = new[] { "Password must be at least 6 characters." };
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!await setupService.IsSetupRequiredAsync(cancellationToken))
        {
            return TypedResults.Conflict(new ApiError(
                "SETUP_COMPLETED",
                "Initial setup has already been completed. Users already exist in the system."));
        }

        var result = await setupService.CompleteSetupAsync(request, cancellationToken);

        if (result == null)
        {
            return TypedResults.Conflict(new ApiError(
                "SETUP_FAILED",
                "Failed to complete setup. Please try again."));
        }

        return TypedResults.Ok(result);
    }
}
