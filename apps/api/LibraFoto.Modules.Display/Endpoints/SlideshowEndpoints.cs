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
    /// Endpoints for slideshow operations.
    /// Provides photo retrieval for the display frontend.
    /// </summary>
    public static class SlideshowEndpoints
    {
        /// <summary>
        /// Maps slideshow endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapSlideshowEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/display/photos")
                .WithTags("Slideshow");

            group.MapGet("/next", GetNextPhoto)
                .WithName("GetNextPhoto")
                .WithSummary("Get the next photo in the slideshow")
                .WithDescription("Returns the next photo based on current display settings. Advances the slideshow sequence.");

            group.MapGet("/current", GetCurrentPhoto)
                .WithName("GetCurrentPhoto")
                .WithSummary("Get the current photo")
                .WithDescription("Returns the currently displayed photo without advancing the sequence.");

            group.MapGet("/preload", GetPreloadPhotos)
                .WithName("GetPreloadPhotos")
                .WithSummary("Get photos for preloading")
                .WithDescription("Returns multiple upcoming photos for frontend caching and preloading.");

            group.MapGet("/count", GetPhotoCount)
                .WithName("GetDisplayPhotoCount")
                .WithSummary("Get total photo count")
                .WithDescription("Returns the total number of photos available for the current slideshow settings.");

            group.MapPost("/reset", ResetSequence)
                .WithName("ResetSlideshowSequence")
                .WithSummary("Reset the slideshow sequence")
                .WithDescription("Resets the slideshow to the beginning. Useful after settings changes.");

            return app;
        }

        /// <summary>
        /// Gets the next photo in the slideshow.
        /// </summary>
        private static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> GetNextPhoto(
            [FromQuery] long? settingsId,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            var photo = await slideshowService.GetNextPhotoAsync(settingsId, cancellationToken);

            if (photo == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "NO_PHOTOS_AVAILABLE",
                    "No photos are available for the current slideshow settings. Add some photos or adjust your filter settings."));
            }

            return TypedResults.Ok(photo);
        }

        /// <summary>
        /// Gets the current photo being displayed.
        /// </summary>
        private static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> GetCurrentPhoto(
            [FromQuery] long? settingsId,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            var photo = await slideshowService.GetCurrentPhotoAsync(settingsId, cancellationToken);

            if (photo == null)
            {
                return TypedResults.NotFound(new ApiError(
                    "NO_PHOTOS_AVAILABLE",
                    "No photos are available for the current slideshow settings."));
            }

            return TypedResults.Ok(photo);
        }

        /// <summary>
        /// Gets photos for preloading.
        /// </summary>
        private static async Task<Ok<IReadOnlyList<PhotoDto>>> GetPreloadPhotos(
            [FromQuery] int? count,
            [FromQuery] long? settingsId,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            var preloadCount = Math.Clamp(count ?? 10, 1, 50);
            var photos = await slideshowService.GetPreloadPhotosAsync(preloadCount, settingsId, cancellationToken);

            return TypedResults.Ok(photos);
        }

        /// <summary>
        /// Gets the total photo count for current settings.
        /// </summary>
        private static async Task<Ok<PhotoCountResponse>> GetPhotoCount(
            [FromQuery] long? settingsId,
            [FromServices] ISlideshowService slideshowService,
            CancellationToken cancellationToken)
        {
            var count = await slideshowService.GetPhotoCountAsync(settingsId, cancellationToken);

            return TypedResults.Ok(new PhotoCountResponse(count));
        }

        /// <summary>
        /// Resets the slideshow sequence.
        /// </summary>
        private static Ok<ResetResponse> ResetSequence(
            [FromQuery] long? settingsId,
            [FromServices] ISlideshowService slideshowService)
        {
            slideshowService.ResetSequence(settingsId);

            return TypedResults.Ok(new ResetResponse(true, "Slideshow sequence has been reset."));
        }
    }

    /// <summary>
    /// Response for photo count endpoint.
    /// </summary>
    public record PhotoCountResponse(int TotalPhotos);

    /// <summary>
    /// Response for reset endpoint.
    /// </summary>
    public record ResetResponse(bool Success, string Message);
}
