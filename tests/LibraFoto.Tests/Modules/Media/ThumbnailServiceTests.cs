using LibraFoto.Modules.Media.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    public class ThumbnailServiceTests
    {
        private ThumbnailService _thumbnailService = null!;
        private string _tempDir = null!;
        private string _testImagePath = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // Create temp directory for thumbnails
            _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoThumbnailTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            _thumbnailService = new ThumbnailService(_tempDir);

            // Create a simple test image
            _testImagePath = Path.Combine(_tempDir, "test_source.jpg");
            await CreateTestImageAsync(_testImagePath);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }

            await Task.CompletedTask;
        }

        [Test]
        [NotInParallel]
        public async Task GenerateThumbnailAsync_FromPath_CreatesThumbnail()
        {
            // Arrange
            var photoId = 1L;
            var dateTaken = new DateTime(2024, 6, 15);

            // Act
            var result = await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Width).IsGreaterThan(0);
            await Assert.That(result.Height).IsGreaterThan(0);
            await Assert.That(result.FileSize).IsGreaterThan(0);
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsTrue();
        }

        [Test]
        [NotInParallel]
        public async Task GenerateThumbnailAsync_FromStream_CreatesThumbnail()
        {
            // Arrange
            var photoId = 2L;
            var dateTaken = new DateTime(2024, 7, 20);
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = await _thumbnailService.GenerateThumbnailAsync(stream, photoId, dateTaken);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Width).IsGreaterThan(0);
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsTrue();
        }

        [Test]
        public async Task ThumbnailExists_ReturnsFalse_WhenNotGenerated()
        {
            // Arrange
            var photoId = 999L;

            // Act
            var exists = _thumbnailService.ThumbnailExists(photoId);

            // Assert
            await Assert.That(exists).IsFalse();
        }

        [Test]
        [NotInParallel]
        public async Task OpenThumbnailStream_ReturnsStream_WhenThumbnailExists()
        {
            // Arrange
            var photoId = 3L;
            var dateTaken = new DateTime(2024, 8, 10);
            await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Act
            using var stream = _thumbnailService.OpenThumbnailStream(photoId);

            // Assert
            await Assert.That(stream).IsNotNull();
            await Assert.That(stream!.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task OpenThumbnailStream_ReturnsNull_WhenThumbnailDoesNotExist()
        {
            // Arrange
            var photoId = 888L;

            // Act
            var stream = _thumbnailService.OpenThumbnailStream(photoId);

            // Assert
            await Assert.That(stream).IsNull();
        }

        [Test]
        [NotInParallel]
        public async Task DeleteThumbnails_RemovesThumbnail()
        {
            // Arrange
            var photoId = 4L;
            var dateTaken = new DateTime(2024, 9, 5);
            await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsTrue();

            // Act
            var deleted = _thumbnailService.DeleteThumbnails(photoId);

            // Assert
            await Assert.That(deleted).IsTrue();
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsFalse();
        }

        [Test]
        public async Task DeleteThumbnails_ReturnsFalse_WhenThumbnailDoesNotExist()
        {
            // Arrange
            var photoId = 777L;

            // Act
            var deleted = _thumbnailService.DeleteThumbnails(photoId);

            // Assert
            await Assert.That(deleted).IsFalse();
        }

        [Test]
        [NotInParallel]
        public async Task GenerateThumbnailAsync_OrganizesByYearMonth()
        {
            // Arrange
            var photoId = 5L;
            var dateTaken = new DateTime(2024, 12, 25);

            // Act
            var result = await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Assert
            await Assert.That(result.AbsolutePath).IsNotNull();
            await Assert.That(result.AbsolutePath!.Contains("2024")).IsTrue();
            await Assert.That(result.AbsolutePath!.Contains("12")).IsTrue();
        }

        [Test]
        [NotInParallel]
        public async Task GenerateThumbnailAsync_CanRegenerateThumbnail()
        {
            // Arrange
            var photoId = 6L;
            var dateTaken = new DateTime(2024, 5, 1);

            // Generate initial thumbnail
            var result1 = await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsTrue();

            // Delete it
            _thumbnailService.DeleteThumbnails(photoId);
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsFalse();

            // Act - regenerate
            var result2 = await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Assert
            await Assert.That(_thumbnailService.ThumbnailExists(photoId)).IsTrue();
            await Assert.That(result2.Width).IsGreaterThan(0);
        }

        [Test]
        [NotInParallel]
        public async Task GetThumbnailPath_ReturnsPath_WhenThumbnailExists()
        {
            // Arrange
            var photoId = 7L;
            var dateTaken = new DateTime(2024, 3, 15);
            await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Act
            var path = _thumbnailService.GetThumbnailPath(photoId);

            // Assert
            await Assert.That(path).IsNotNull();
            await Assert.That(path).Contains($"{photoId}.jpg");
        }

        [Test]
        [NotInParallel]
        public async Task GetThumbnailAbsolutePath_ReturnsAbsolutePath_WhenThumbnailExists()
        {
            // Arrange
            var photoId = 8L;
            var dateTaken = new DateTime(2024, 4, 20);
            await _thumbnailService.GenerateThumbnailAsync(_testImagePath, photoId, dateTaken);

            // Act
            var absolutePath = _thumbnailService.GetThumbnailAbsolutePath(photoId);

            // Assert
            await Assert.That(absolutePath).IsNotNull();
            await Assert.That(Path.IsPathRooted(absolutePath!)).IsTrue();
            await Assert.That(File.Exists(absolutePath!)).IsTrue();
        }

        /// <summary>
        /// Creates a minimal valid JPEG image for testing.
        /// </summary>
        private static async Task CreateTestImageAsync(string path)
        {
            using var image = new Image<Rgba32>(100, 100, Color.Blue);
            await image.SaveAsync(path, new JpegEncoder());
        }
    }
}
