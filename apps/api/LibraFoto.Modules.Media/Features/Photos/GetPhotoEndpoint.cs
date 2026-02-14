using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Media.Services.Repositories;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Features.Photos;

/// <summary>
/// Serve full-size photo or video file.
/// </summary>
public sealed class GetPhotoEndpoint : EndpointWithoutRequest<Results<FileStreamHttpResult, NotFound>>
{
    public override void Configure()
    {
        Get("/api/media/photos/{photoId:long}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get the full-size photo or video file";
            s.Description = "Get the full-size photo or video file";
        });
    }

    public override async Task<Results<FileStreamHttpResult, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var mediaPhotoRepository = Resolve<IMediaPhotoRepository>();
        var configuration = Resolve<IConfiguration>();
        var photoId = Route<long>("photoId");

        var photo = await mediaPhotoRepository.GetPhotoByIdAsync(photoId, ct);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
            var absolutePath = Path.Combine(storagePath, photo.FilePath);

            if (!File.Exists(absolutePath))
            {
                return TypedResults.NotFound();
            }

            var fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var contentType = GetContentTypeFromFilename(photo.Filename, photo.MediaType);
            return TypedResults.File(fileStream, contentType, enableRangeProcessing: true);
        }
        catch
        {
            return TypedResults.NotFound();
        }
    }

    private static string GetContentTypeFromFilename(string filename, MediaType mediaType)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        if (mediaType == MediaType.Video)
        {
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".webm" => "video/webm",
                _ => "video/mp4"
            };
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
    }
}
