using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Auth.Endpoints;

/// <summary>
/// Setup endpoints for initial application configuration.
/// </summary>
public static class SetupEndpoints
{
    /// <summary>
    /// Maps setup endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/setup")
            .WithTags("Setup")
            .AllowAnonymous();

        group.MapGet("/status", GetSetupStatus)
            .WithName("GetSetupStatus")
            .WithSummary("Check setup status")
            .WithDescription("Checks if initial setup is required (no users exist).");

        group.MapPost("/complete", CompleteSetup)
            .WithName("CompleteSetup")
            .WithSummary("Complete initial setup")
            .WithDescription("Creates the first admin user. Only works if no users exist.");

        return app;
    }

    /// <summary>
    /// Gets the current setup status.
    /// </summary>
    private static async Task<Ok<SetupStatusResponse>> GetSetupStatus(
        [FromServices] ISetupService setupService,
        CancellationToken cancellationToken)
    {
        var status = await setupService.GetSetupStatusAsync(cancellationToken);
        return TypedResults.Ok(status);
    }

    /// <summary>
    /// Completes the initial setup by creating the first admin user.
    /// </summary>
    private static async Task<Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>> CompleteSetup(
        [FromBody] SetupRequest request,
        [FromServices] ISetupService setupService,
        CancellationToken cancellationToken)
    {
        // Validate request
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

        // Check if setup is still required
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
