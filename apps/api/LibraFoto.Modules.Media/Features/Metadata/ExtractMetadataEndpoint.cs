using FastEndpoints;
using LibraFoto.Modules.Media.Services.Shared;
using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Media.Features.Metadata;

/// <summary>
/// Extract metadata from an uploaded image.
/// </summary>
public sealed class ExtractMetadataEndpoint : Endpoint<ExtractMetadataRequest, Results<Ok<MetadataResponse>, BadRequest<string>>>
{
    public override void Configure()
    {
        Post("/api/media/metadata/extract");
        AllowAnonymous();
        AllowFileUploads();
        Tags("Metadata");
        Summary(s =>
        {
            s.Summary = "Extract metadata from an uploaded image";
            s.Description = "Extract metadata from an uploaded image";
        });
    }

    public override async Task<Results<Ok<MetadataResponse>, BadRequest<string>>> ExecuteAsync(
        ExtractMetadataRequest req,
        CancellationToken ct)
    {
        var metadataService = Resolve<IMetadataService>();
        var geocodingService = Resolve<IGeocodingService>();
        var file = req.File ?? Files.FirstOrDefault();
        return await HandleRequestAsync(file, metadataService, geocodingService, ct);
    }

    internal static async Task<Results<Ok<MetadataResponse>, BadRequest<string>>> HandleRequestAsync(
        IFormFile? file,
        IMetadataService metadataService,
        IGeocodingService geocodingService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
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

public sealed class ExtractMetadataRequest
{
    public IFormFile? File { get; init; }
}

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
