using FastEndpoints;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Media.Features.Metadata;

/// <summary>
/// Extract metadata from a server-side file path.
/// </summary>
public sealed class ExtractMetadataFromPathEndpoint : Endpoint<ExtractMetadataFromPathRequest, Results<Ok<MetadataResponse>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Get("/api/media/metadata/file");
        Policies("Authenticated");
        Tags("Metadata");
        Summary(s =>
        {
            s.Summary = "Extract metadata from a file path";
            s.Description = "Extract metadata from a file path";
        });
    }

    public override async Task<Results<Ok<MetadataResponse>, NotFound, BadRequest<string>>> ExecuteAsync(
        ExtractMetadataFromPathRequest req,
        CancellationToken ct)
    {
        var metadataService = Resolve<IMetadataService>();
        var geocodingService = Resolve<IGeocodingService>();
        return await HandleRequestAsync(req.Path, metadataService, geocodingService, ct);
    }

    internal static async Task<Results<Ok<MetadataResponse>, NotFound, BadRequest<string>>> HandleRequestAsync(
        string? path,
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

public sealed class ExtractMetadataFromPathRequest
{
    [QueryParam]
    public string? Path { get; init; }
}
