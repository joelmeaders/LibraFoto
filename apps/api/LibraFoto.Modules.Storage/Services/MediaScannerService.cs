using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Services
{
    /// <summary>
    /// Service for scanning directories and identifying media files.
    /// </summary>
    public class MediaScannerService : IMediaScannerService
    {
        private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".avif"
        };

        private static readonly HashSet<string> _videoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".3gp", ".wmv", ".flv"
        };

        private static readonly Dictionary<string, string> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".webp", "image/webp" },
            { ".bmp", "image/bmp" },
            { ".tiff", "image/tiff" },
            { ".tif", "image/tiff" },
            { ".heic", "image/heic" },
            { ".heif", "image/heif" },
            { ".avif", "image/avif" },
            // Videos
            { ".mp4", "video/mp4" },
            { ".mov", "video/quicktime" },
            { ".avi", "video/x-msvideo" },
            { ".mkv", "video/x-matroska" },
            { ".webm", "video/webm" },
            { ".m4v", "video/x-m4v" },
            { ".3gp", "video/3gpp" },
            { ".wmv", "video/x-ms-wmv" },
            { ".flv", "video/x-flv" }
        };

        /// <inheritdoc />
        public IReadOnlySet<string> SupportedImageExtensions => _imageExtensions;

        /// <inheritdoc />
        public IReadOnlySet<string> SupportedVideoExtensions => _videoExtensions;

        /// <inheritdoc />
        public async Task<IEnumerable<ScannedFile>> ScanDirectoryAsync(
            string directoryPath,
            bool recursive = true,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ScannedFile>();

            if (!Directory.Exists(directoryPath))
            {
                return results;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            await Task.Run(() =>
            {
                try
                {
                    var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption);

                    foreach (var filePath in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!IsSupportedMediaFile(filePath))
                        {
                            continue;
                        }

                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            var extension = fileInfo.Extension.ToLowerInvariant();
                            var relativePath = Path.GetRelativePath(directoryPath, filePath);

                            results.Add(new ScannedFile
                            {
                                FullPath = filePath,
                                RelativePath = relativePath,
                                FileName = fileInfo.Name,
                                Extension = extension,
                                FileSize = fileInfo.Length,
                                ContentType = GetContentType(filePath),
                                MediaType = IsSupportedImage(filePath) ? MediaType.Photo : MediaType.Video,
                                CreatedTime = fileInfo.CreationTimeUtc,
                                ModifiedTime = fileInfo.LastWriteTimeUtc,
                                IsHidden = (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip files we can't access
                        }
                        catch (IOException)
                        {
                            // Skip files with IO errors
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't access directory
                }
            }, cancellationToken);

            return results;
        }

        /// <inheritdoc />
        public bool IsSupportedMediaFile(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(extension) &&
                   (_imageExtensions.Contains(extension) || _videoExtensions.Contains(extension));
        }

        /// <inheritdoc />
        public bool IsSupportedImage(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(extension) && _imageExtensions.Contains(extension);
        }

        /// <inheritdoc />
        public bool IsSupportedVideo(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(extension) && _videoExtensions.Contains(extension);
        }

        /// <inheritdoc />
        public string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return _contentTypes.TryGetValue(extension, out var contentType)
                ? contentType
                : "application/octet-stream";
        }

        /// <inheritdoc />
        public string GenerateUniqueFilename(string originalFilename, string targetDirectory)
        {
            var filename = Path.GetFileNameWithoutExtension(originalFilename);
            var extension = Path.GetExtension(originalFilename);

            // Sanitize filename - remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            filename = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = "photo";
            }

            var candidate = $"{filename}{extension}";
            var candidatePath = Path.Combine(targetDirectory, candidate);

            if (!File.Exists(candidatePath))
            {
                return candidate;
            }

            // Add timestamp and counter for uniqueness
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var counter = 1;

            while (File.Exists(candidatePath))
            {
                candidate = $"{filename}_{timestamp}_{counter:D3}{extension}";
                candidatePath = Path.Combine(targetDirectory, candidate);
                counter++;

                if (counter > 999)
                {
                    // Fallback to GUID if too many collisions
                    candidate = $"{filename}_{Guid.NewGuid():N}{extension}";
                    break;
                }
            }

            return candidate;
        }
    }
}
