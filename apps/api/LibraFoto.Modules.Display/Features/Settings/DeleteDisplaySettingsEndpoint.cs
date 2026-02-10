using FastEndpoints;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Display.Features.Settings;

/// <summary>
/// Delete display settings.
/// </summary>
public sealed class DeleteDisplaySettingsEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Delete("/api/display/settings/{id:long}");
        AllowAnonymous();
        Tags("Display Settings");
        Summary(s =>
        {
            s.Summary = "Delete display settings";
            s.Description = "Deletes a display settings configuration.";
        });
    }

    public override async Task<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(CancellationToken ct)
    {
        var settingsService = Resolve<IDisplaySettingsService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, settingsService, ct);
    }

    internal static async Task<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long id,
        IDisplaySettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var deleted = await settingsService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            var exists = await settingsService.GetByIdAsync(id, cancellationToken);
            if (exists == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "SETTINGS_NOT_FOUND",
                    $"Display settings with ID {id} not found."));
            }

            return TypedResults.BadRequest(new ApiError(
                "CANNOT_DELETE_LAST",
                "Cannot delete the last display settings configuration."));
        }

        return TypedResults.NoContent();
    }
}
