using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Media.Endpoints
{
    /// <summary>
    /// Endpoints for serving photo and video files.
    /// </summary>
    public static class PhotoEndpoints
    {
        public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/media/photos");

            group.MapGet("/{photoId:long}", GetPhoto)
                .WithName("GetPhoto")
                .WithDescription("Get the full-size photo or video file");

            return app;
        }

        /// <summary>
        /// Gets the full-size photo or video file.
        /// </summary>
        private static async Task<Results<FileStreamHttpResult, NotFound>> GetPhoto(
            long photoId,
            LibraFotoDbContext dbContext,
            IConfiguration configuration,
            CancellationToken ct)
        {
            var photo = await dbContext.Photos.FindAsync([photoId], ct);
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

        /// <summary>
        /// Gets the content type from the filename and media type.
        /// </summary>
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

            // Photo
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
}
