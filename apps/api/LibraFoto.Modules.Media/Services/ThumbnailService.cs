using LibraFoto.Modules.Media.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Media.Services
{
    /// <summary>
    /// Service for generating 400x400 thumbnails from images using ImageSharp.
    /// </summary>
    public class ThumbnailService : IThumbnailService
    {
        private const int ThumbnailSize = 400;
        private const int ThumbnailQuality = 85;

        private readonly string _thumbnailBasePath;

        public ThumbnailService(string? thumbnailBasePath = null)
        {
            _thumbnailBasePath = thumbnailBasePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".thumbnails");
            Directory.CreateDirectory(_thumbnailBasePath);
        }

        public string ThumbnailBasePath => _thumbnailBasePath;

        public async Task<ThumbnailResult> GenerateThumbnailAsync(
            Stream sourceStream,
            long photoId,
            DateTime dateTaken,
            CancellationToken cancellationToken = default)
        {
            using var image = await Image.LoadAsync(sourceStream, cancellationToken);
            return await GenerateThumbnailInternalAsync(image, photoId, dateTaken, cancellationToken);
        }

        public async Task<ThumbnailResult> GenerateThumbnailAsync(
            string sourcePath,
            long photoId,
            DateTime dateTaken,
            CancellationToken cancellationToken = default)
        {
            using var image = await Image.LoadAsync(sourcePath, cancellationToken);
            return await GenerateThumbnailInternalAsync(image, photoId, dateTaken, cancellationToken);
        }

        private async Task<ThumbnailResult> GenerateThumbnailInternalAsync(
            Image image,
            long photoId,
            DateTime dateTaken,
            CancellationToken cancellationToken)
        {
            var thumbnailPath = GetThumbnailFilePath(photoId, dateTaken);

            // Create a clone to avoid modifying the original
            using var thumbnail = image.Clone(ctx => ctx
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(ThumbnailSize, ThumbnailSize),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

            var encoder = new JpegEncoder { Quality = ThumbnailQuality };

            await thumbnail.SaveAsync(thumbnailPath, encoder, cancellationToken);

            var fileInfo = new FileInfo(thumbnailPath);
            return ThumbnailResult.Successful(
                path: GetRelativePath(thumbnailPath),
                absolutePath: thumbnailPath,
                width: thumbnail.Width,
                height: thumbnail.Height,
                fileSize: fileInfo.Length
            );
        }

        public string? GetThumbnailPath(long photoId)
        {
            var path = FindThumbnailFilePath(photoId);
            return path != null && File.Exists(path) ? GetRelativePath(path) : null;
        }

        public string? GetThumbnailAbsolutePath(long photoId)
        {
            var path = FindThumbnailFilePath(photoId);
            return path != null && File.Exists(path) ? path : null;
        }

        public Stream? OpenThumbnailStream(long photoId)
        {
            var path = FindThumbnailFilePath(photoId);
            return path != null && File.Exists(path) ? File.OpenRead(path) : null;
        }

        public bool ThumbnailExists(long photoId)
        {
            var path = FindThumbnailFilePath(photoId);
            return path != null && File.Exists(path);
        }

        public bool DeleteThumbnails(long photoId)
        {
            var path = FindThumbnailFilePath(photoId);
            if (path != null && File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }

        private string GetThumbnailFilePath(long photoId, DateTime dateTaken)
        {
            // Use dateTaken to determine year/month path structure
            var yearMonth = Path.Combine(dateTaken.Year.ToString(), dateTaken.Month.ToString("D2"));
            var directory = Path.Combine(_thumbnailBasePath, yearMonth);

            Directory.CreateDirectory(directory);

            return Path.Combine(directory, $"{photoId}.jpg");
        }

        /// <summary>
        /// Finds a thumbnail file by searching the directory structure.
        /// </summary>
        private string? FindThumbnailFilePath(long photoId)
        {
            var pattern = $"{photoId}.jpg";

            if (!Directory.Exists(_thumbnailBasePath))
            {
                return null;
            }

            // Search all subdirectories for the thumbnail
            var files = Directory.GetFiles(_thumbnailBasePath, pattern, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        private string GetRelativePath(string absolutePath)
        {
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), absolutePath);
        }
    }
}
