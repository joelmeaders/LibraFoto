using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Admin.Endpoints;

/// <summary>
/// Endpoints for system information and updates.
/// </summary>
public static class SystemEndpoints
{
    /// <summary>
    /// Maps system endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system")
            .WithTags("System");

        group.MapGet("/info", GetSystemInfo)
            .WithName("GetSystemInfo")
            .WithSummary("Get system information")
            .WithDescription("Returns current system information including version, uptime, and update status.");

        group.MapGet("/updates", CheckForUpdates)
            .WithName("CheckForUpdates")
            .WithSummary("Check for available updates")
            .WithDescription("Checks the remote repository for available updates.");

        group.MapPost("/updates/check", ForceCheckForUpdates)
            .WithName("ForceCheckForUpdates")
            .WithSummary("Force check for updates")
            .WithDescription("Forces a fresh check for updates, bypassing the cache.");

        group.MapPost("/update", TriggerUpdate)
            .WithName("TriggerUpdate")
            .WithSummary("Trigger application update")
            .WithDescription("Triggers the update process. The application will restart after updating.");

        return app;
    }

    /// <summary>
    /// Gets current system information.
    /// </summary>
    private static async Task<Ok<SystemInfoResponse>> GetSystemInfo(
        [FromServices] ISystemService systemService,
        CancellationToken cancellationToken)
    {
        var info = await systemService.GetSystemInfoAsync(cancellationToken);
        return TypedResults.Ok(info);
    }

    /// <summary>
    /// Checks for available updates (uses cached result if available).
    /// </summary>
    private static async Task<Ok<UpdateCheckResponse>> CheckForUpdates(
        [FromServices] ISystemService systemService,
        CancellationToken cancellationToken)
    {
        var result = await systemService.CheckForUpdatesAsync(forceRefresh: false, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Forces a fresh check for updates.
    /// </summary>
    private static async Task<Ok<UpdateCheckResponse>> ForceCheckForUpdates(
        [FromServices] ISystemService systemService,
        CancellationToken cancellationToken)
    {
        var result = await systemService.CheckForUpdatesAsync(forceRefresh: true, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Triggers the application update process.
    /// </summary>
    private static async Task<Accepted<UpdateTriggerResponse>> TriggerUpdate(
        [FromServices] ISystemService systemService,
        CancellationToken cancellationToken)
    {
        var result = await systemService.TriggerUpdateAsync(cancellationToken);
        return TypedResults.Accepted((string?)null, result);
    }
}
