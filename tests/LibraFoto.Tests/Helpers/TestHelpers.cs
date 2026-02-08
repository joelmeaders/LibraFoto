using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Tests.Helpers
{
    /// <summary>
    /// Helper methods for creating test data.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Creates a test photo entity.
        /// </summary>
        public static Photo CreateTestPhoto(
            long id = 1,
            string filename = "test.jpg",
            MediaType mediaType = MediaType.Photo,
            long? providerId = null) =>
            new()
            {
                Id = id,
                Filename = filename,
                OriginalFilename = filename,
                FilePath = $"2026/01/{filename}",
                FileSize = 1000,
                Width = 800,
                Height = 600,
                MediaType = mediaType,
                DateAdded = DateTime.UtcNow,
                ProviderId = providerId,
                ProviderFileId = providerId.HasValue ? $"provider-{id}" : null
            };

        /// <summary>
        /// Creates a test storage provider entity.
        /// </summary>
        public static StorageProvider CreateTestStorageProvider(
            long id = 1,
            string name = "Test Provider",
            StorageProviderType type = StorageProviderType.Local,
            bool isEnabled = true) =>
            new()
            {
                Id = id,
                Name = name,
                Type = type,
                IsEnabled = isEnabled,
                Configuration = type == StorageProviderType.Local
                    ? """{"BasePath":"./test-photos","OrganizeByDate":true,"WatchForChanges":false}"""
                    : null
            };

        /// <summary>
        /// Creates a test storage file info.
        /// </summary>
        public static StorageFileInfo CreateTestStorageFileInfo(
            string fileId = "2026/01/test.jpg",
            string fileName = "test.jpg",
            MediaType mediaType = MediaType.Photo,
            long fileSize = 1000) =>
            new()
            {
                FileId = fileId,
                FileName = fileName,
                FullPath = $"/photos/{fileId}",
                FileSize = fileSize,
                ContentType = mediaType == MediaType.Photo ? "image/jpeg" : "video/mp4",
                MediaType = mediaType,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                IsFolder = false
            };

        /// <summary>
        /// Creates a test scanned file.
        /// </summary>
        public static ScannedFile CreateTestScannedFile(
            string fileName = "test.jpg",
            string relativePath = "2026/01/test.jpg",
            MediaType mediaType = MediaType.Photo,
            long fileSize = 1000) =>
            new()
            {
                FullPath = $"/photos/{relativePath}",
                RelativePath = relativePath,
                FileName = fileName,
                Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                FileSize = fileSize,
                ContentType = mediaType == MediaType.Photo ? "image/jpeg" : "video/mp4",
                MediaType = mediaType,
                CreatedTime = DateTime.UtcNow,
                ModifiedTime = DateTime.UtcNow,
                IsHidden = false
            };

        /// <summary>
        /// Creates a temporary directory for testing and returns its path.
        /// </summary>
        public static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "librafoto-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Cleans up a temporary directory.
        /// </summary>
        public static void CleanupTempDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        /// <summary>
        /// Creates a test file with random content.
        /// </summary>
        public static string CreateTestFile(string directory, string fileName, int sizeKb = 1)
        {
            var filePath = Path.Combine(directory, fileName);
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var content = new byte[sizeKb * 1024];
            Random.Shared.NextBytes(content);
            File.WriteAllBytes(filePath, content);
            return filePath;
        }

        /// <summary>
        /// Creates a test photo entity with corresponding physical file on disk.
        /// </summary>
        public static (Photo photo, string filePath) CreateTestPhotoWithFile(
            string storageDirectory,
            long id = 1,
            string filename = "test.jpg",
            MediaType mediaType = MediaType.Photo,
            long? providerId = null)
        {
            var yearMonth = Path.Combine("2026", "01");
            var mediaDir = Path.Combine(storageDirectory, "media", yearMonth);
            Directory.CreateDirectory(mediaDir);

            var filePath = Path.Combine(mediaDir, filename);
            var content = new byte[1024];
            Random.Shared.NextBytes(content);
            File.WriteAllBytes(filePath, content);

            var relativePath = Path.Combine("media", yearMonth, filename).Replace('\\', '/');

            var photo = new Photo
            {
                Id = id,
                Filename = filename,
                OriginalFilename = filename,
                FilePath = relativePath,
                FileSize = 1000,
                Width = 800,
                Height = 600,
                MediaType = mediaType,
                DateAdded = DateTime.UtcNow,
                DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                ProviderId = providerId,
                ProviderFileId = providerId.HasValue ? $"provider-{id}" : null
            };

            return (photo, filePath);
        }

        /// <summary>
        /// Creates a test photo entity with both physical file and thumbnail on disk.
        /// </summary>
        public static (Photo photo, string filePath, string thumbnailPath) CreateTestPhotoWithThumbnail(
            string storageDirectory,
            long id = 1,
            string filename = "test.jpg")
        {
            var (photo, filePath) = CreateTestPhotoWithFile(storageDirectory, id, filename);

            // Create thumbnail
            var yearMonth = Path.Combine("2026", "01");
            var thumbnailDir = Path.Combine(storageDirectory, ".thumbnails", yearMonth);
            Directory.CreateDirectory(thumbnailDir);

            var thumbnailPath = Path.Combine(thumbnailDir, $"{id}.jpg");
            var thumbnailContent = new byte[512];
            Random.Shared.NextBytes(thumbnailContent);
            File.WriteAllBytes(thumbnailPath, thumbnailContent);

            var relativeThumbnailPath = Path.Combine(".thumbnails", yearMonth, $"{id}.jpg").Replace('\\', '/');
            photo.ThumbnailPath = relativeThumbnailPath;

            return (photo, filePath, thumbnailPath);
        }

        /// <summary>
        /// Creates a test album entity.
        /// </summary>
        public static Album CreateTestAlbum(
            long id = 1,
            string name = "Test Album",
            long? coverPhotoId = null) =>
            new()
            {
                Id = id,
                Name = name,
                Description = "Test album description",
                CoverPhotoId = coverPhotoId,
                DateCreated = DateTime.UtcNow,
                SortOrder = 0
            };

        /// <summary>
        /// Creates a test tag entity.
        /// </summary>
        public static Tag CreateTestTag(
            long id = 1,
            string? name = null,
            string? color = "#FF5733") =>
            new()
            {
                Id = id,
                Name = name ?? $"Test Tag {id}",
                Color = color
            };
    }
}
