using LibraFoto.Modules.Media.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    public class MetadataServiceTests
    {
        private MetadataService _service = null!;
        private string _tempDir = null!;
        private string _testImagePath = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _service = new MetadataService();
            _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoMetadataTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = Path.Combine(_tempDir, "test.jpg");
            await CreateTestImageAsync(_testImagePath);
        }

        [After(Test)]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public async Task ExtractMetadata_FromFilePath_ReturnsMetadata()
        {
            // Act
            var result = _service.ExtractMetadata(_testImagePath);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        [Test]
        public async Task ExtractMetadata_FromStream_ReturnsMetadata()
        {
            // Arrange
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = _service.ExtractMetadata(stream, "test.jpg");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        [Test]
        public async Task ExtractMetadataAsync_FromFilePath_ReturnsMetadata()
        {
            // Act
            var result = await _service.ExtractMetadataAsync(_testImagePath);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        [Test]
        public async Task ExtractMetadataAsync_FromStream_ReturnsMetadata()
        {
            // Arrange
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = await _service.ExtractMetadataAsync(stream, "test.jpg");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        [Test]
        public async Task ExtractDateTaken_ReturnsNull_WhenNoExif()
        {
            // Arrange
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = _service.ExtractDateTaken(stream);

            // Assert
            // Simple test image won't have EXIF date
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task ExtractGpsCoordinates_ReturnsNull_WhenNoGps()
        {
            // Arrange
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = _service.ExtractGpsCoordinates(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task ExtractDimensions_ReturnsDimensions()
        {
            // Arrange
            await using var stream = File.OpenRead(_testImagePath);

            // Act
            var result = _service.ExtractDimensions(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Width).IsEqualTo(100);
            await Assert.That(result.Value.Height).IsEqualTo(100);
        }

        [Test]
        public async Task ExtractMetadata_HandlesInvalidFile_Gracefully()
        {
            // Arrange
            var invalidPath = Path.Combine(_tempDir, "nonexistent.jpg");

            // Act
            var result = _service.ExtractMetadata(invalidPath);

            // Assert - Should return empty metadata, not throw
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsFalse();
        }

        [Test]
        public async Task ExtractMetadata_HandlesInvalidStream_Gracefully()
        {
            // Arrange
            await using var stream = new MemoryStream(new byte[] { 0, 1, 2, 3 });

            // Act
            var result = _service.ExtractMetadata(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsFalse();
        }

        private static async Task CreateTestImageAsync(string path)
        {
            using var image = new Image<Rgba32>(100, 100, Color.Blue);
            await image.SaveAsync(path, new JpegEncoder());
        }
    }
}