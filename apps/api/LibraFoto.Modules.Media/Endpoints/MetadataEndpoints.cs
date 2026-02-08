using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Media.Endpoints
{
    /// <summary>
    /// Endpoints for extracting image metadata.
    /// </summary>
    public static class MetadataEndpoints
    {
        public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/media/metadata");

            group.MapPost("/extract", ExtractMetadataFromUpload)
                .WithName("ExtractMetadata")
                .WithDescription("Extract metadata from an uploaded image")
                .DisableAntiforgery();

            group.MapGet("/file", ExtractMetadataFromPath)
                .WithName("ExtractMetadataFromPath")
                .WithDescription("Extract metadata from a file path")
                .RequireAuthorization();

            return app;
        }

        /// <summary>
        /// Extracts metadata from an uploaded image file.
        /// </summary>
        private static async Task<Results<Ok<MetadataResponse>, BadRequest<string>>> ExtractMetadataFromUpload(
            IFormFile file,
            IMetadataService metadataService,
            IGeocodingService geocodingService,
            CancellationToken cancellationToken)
        {
            if (file.Length == 0)
            {
                return TypedResults.BadRequest("No file uploaded.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var metadata = await metadataService.ExtractMetadataAsync(stream, file.FileName, cancellationToken);

                string? locationName = null;
                if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
                {
                    var geocodingResult = await geocodingService.ReverseGeocodeAsync(
                        metadata.Latitude.Value,
                        metadata.Longitude.Value,
                        cancellationToken);

                    locationName = geocodingResult?.DisplayName;
                }

                var response = MapToResponse(metadata, locationName);
                return TypedResults.Ok(response);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest($"Failed to extract metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts metadata from a file path on the server.
        /// </summary>
        private static async Task<Results<Ok<MetadataResponse>, NotFound, BadRequest<string>>> ExtractMetadataFromPath(
            string path,
            IMetadataService metadataService,
            IGeocodingService geocodingService,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return TypedResults.BadRequest("Path is required.");
            }

            if (!File.Exists(path))
            {
                return TypedResults.NotFound();
            }

            try
            {
                var metadata = await metadataService.ExtractMetadataAsync(path, cancellationToken);

                string? locationName = null;
                if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
                {
                    var geocodingResult = await geocodingService.ReverseGeocodeAsync(
                        metadata.Latitude.Value,
                        metadata.Longitude.Value,
                        cancellationToken);

                    locationName = geocodingResult?.DisplayName;
                }

                var response = MapToResponse(metadata, locationName);
                return TypedResults.Ok(response);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest($"Failed to extract metadata: {ex.Message}");
            }
        }

        private static MetadataResponse MapToResponse(ImageMetadata metadata, string? locationName) => new(
            Width: metadata.Width,
            Height: metadata.Height,
            DateTaken: metadata.DateTaken,
            CameraMake: metadata.CameraMake,
            CameraModel: metadata.CameraModel,
            LensModel: metadata.LensModel,
            FocalLength: metadata.FocalLength,
            Aperture: metadata.Aperture,
            ExposureTime: metadata.ShutterSpeedFormatted,
            Iso: metadata.Iso,
            Latitude: metadata.Latitude,
            Longitude: metadata.Longitude,
            Altitude: metadata.Altitude,
            Orientation: metadata.Orientation,
            ColorSpace: metadata.ColorSpace,
            LocationName: locationName
        );
    }

    /// <summary>
    /// Response containing extracted image metadata.
    /// </summary>
    public record MetadataResponse(
        int? Width,
        int? Height,
        DateTime? DateTaken,
        string? CameraMake,
        string? CameraModel,
        string? LensModel,
        double? FocalLength,
        double? Aperture,
        string? ExposureTime,
        int? Iso,
        double? Latitude,
        double? Longitude,
        double? Altitude,
        int? Orientation,
        string? ColorSpace,
        string? LocationName
    );
}
