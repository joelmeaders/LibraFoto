using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Display.Endpoints
{
    /// <summary>
    /// Endpoints for display settings management.
    /// </summary>
    public static class DisplaySettingsEndpoints
    {
        /// <summary>
        /// Maps display settings endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapDisplaySettingsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/display/settings")
                .WithTags("Display Settings");

            group.MapGet("/", GetActiveSettings)
                .WithName("GetActiveDisplaySettings")
                .WithSummary("Get active display settings")
                .WithDescription("Returns the currently active display settings configuration.");

            group.MapGet("/all", GetAllSettings)
                .WithName("GetAllDisplaySettings")
                .WithSummary("Get all display settings")
                .WithDescription("Returns all display settings configurations.");

            group.MapGet("/{id:long}", GetSettingsById)
                .WithName("GetDisplaySettingsById")
                .WithSummary("Get display settings by ID")
                .WithDescription("Returns a specific display settings configuration.");

            group.MapPut("/{id:long}", UpdateSettings)
                .WithName("UpdateDisplaySettings")
                .WithSummary("Update display settings")
                .WithDescription("Updates an existing display settings configuration.");

            group.MapPost("/", CreateSettings)
                .WithName("CreateDisplaySettings")
                .WithSummary("Create display settings")
                .WithDescription("Creates a new display settings configuration.");

            group.MapDelete("/{id:long}", DeleteSettings)
                .WithName("DeleteDisplaySettings")
                .WithSummary("Delete display settings")
                .WithDescription("Deletes a display settings configuration.");

            group.MapPost("/{id:long}/activate", ActivateSettings)
                .WithName("ActivateDisplaySettings")
                .WithSummary("Activate display settings")
                .WithDescription("Sets a display settings configuration as the active one.");

            return app;
        }

        /// <summary>
        /// Gets the active display settings.
        /// </summary>
        private static async Task<Ok<DisplaySettingsDto>> GetActiveSettings(
            [FromServices] IDisplaySettingsService settingsService,
            CancellationToken cancellationToken)
        {
            var settings = await settingsService.GetActiveSettingsAsync(cancellationToken);
            return TypedResults.Ok(settings);
        }

        /// <summary>
        /// Gets all display settings.
        /// </summary>
        private static async Task<Ok<IReadOnlyList<DisplaySettingsDto>>> GetAllSettings(
            [FromServices] IDisplaySettingsService settingsService,
            CancellationToken cancellationToken)
        {
            var settings = await settingsService.GetAllAsync(cancellationToken);
            return TypedResults.Ok(settings);
        }

        /// <summary>
        /// Gets display settings by ID.
        /// </summary>
        private static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> GetSettingsById(
            long id,
            [FromServices] IDisplaySettingsService settingsService,
            CancellationToken cancellationToken)
        {
            var settings = await settingsService.GetByIdAsync(id, cancellationToken);

            if (settings == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "SETTINGS_NOT_FOUND",
                    $"Display settings with ID {id} not found."));
            }

            return TypedResults.Ok(settings);
        }

        /// <summary>
        /// Updates display settings.
        /// </summary>
        private static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>, BadRequest<ApiError>>> UpdateSettings(
            long id,
            [FromBody] UpdateDisplaySettingsRequest request,
            [FromServices] IDisplaySettingsService settingsService,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            // Validate slide duration
            if (request.SlideDuration.HasValue && request.SlideDuration.Value < 1)
            {
                return TypedResults.BadRequest(new ApiError(
                    "VALIDATION_ERROR",
                    "Slide duration must be at least 1 second."));
            }

            // Validate transition duration
            if (request.TransitionDuration.HasValue && request.TransitionDuration.Value < 0)
            {
                return TypedResults.BadRequest(new ApiError(
                    "VALIDATION_ERROR",
                    "Transition duration cannot be negative."));
            }

            var settings = await settingsService.UpdateAsync(id, request, cancellationToken);

            if (settings == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "SETTINGS_NOT_FOUND",
                    $"Display settings with ID {id} not found."));
            }

            // Reset slideshow sequence when settings change
            slideshowService.ResetSequence(id);

            return TypedResults.Ok(settings);
        }

        /// <summary>
        /// Creates new display settings.
        /// </summary>
        private static async Task<Results<Created<DisplaySettingsDto>, BadRequest<ApiError>>> CreateSettings(
            [FromBody] UpdateDisplaySettingsRequest request,
            [FromServices] IDisplaySettingsService settingsService,
            CancellationToken cancellationToken)
        {
            // Validate slide duration
            if (request.SlideDuration.HasValue && request.SlideDuration.Value < 1)
            {
                return TypedResults.BadRequest(new ApiError(
                    "VALIDATION_ERROR",
                    "Slide duration must be at least 1 second."));
            }

            // Validate transition duration
            if (request.TransitionDuration.HasValue && request.TransitionDuration.Value < 0)
            {
                return TypedResults.BadRequest(new ApiError(
                    "VALIDATION_ERROR",
                    "Transition duration cannot be negative."));
            }

            var settings = await settingsService.CreateAsync(request, cancellationToken);

            return TypedResults.Created($"/api/display/settings/{settings.Id}", settings);
        }

        /// <summary>
        /// Deletes display settings.
        /// </summary>
        private static async Task<Results<NoContent, NotFound<ApiError>, BadRequest<ApiError>>> DeleteSettings(
            long id,
            [FromServices] IDisplaySettingsService settingsService,
            CancellationToken cancellationToken)
        {
            var deleted = await settingsService.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                // Check if it exists
                var exists = await settingsService.GetByIdAsync(id, cancellationToken);
                if (exists == null)
                {
                    return TypedResults.NotFound(new ApiError(
                        "SETTINGS_NOT_FOUND",
                        $"Display settings with ID {id} not found."));
                }

                // Exists but couldn't delete - must be the last one
                return TypedResults.BadRequest(new ApiError(
                    "CANNOT_DELETE_LAST",
                    "Cannot delete the last display settings configuration."));
            }

            return TypedResults.NoContent();
        }

        /// <summary>
        /// Activates display settings.
        /// </summary>
        private static async Task<Results<Ok<DisplaySettingsDto>, NotFound<ApiError>>> ActivateSettings(
            long id,
            [FromServices] IDisplaySettingsService settingsService,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            var settings = await settingsService.SetActiveAsync(id, cancellationToken);

            if (settings == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "SETTINGS_NOT_FOUND",
                    $"Display settings with ID {id} not found."));
            }

            // Reset slideshow to use new active settings
            slideshowService.ResetSequence(null);

            return TypedResults.Ok(settings);
        }
    }
}
