using LibraFoto.Modules.Storage.Services;
using LibraFoto.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Tests for ImageImportService.
    /// </summary>
    public class ImageImportServiceTests
    {
        private string _tempDir = null!;
        private ImageImportService _service = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _tempDir = TestHelpers.CreateTempDirectory();
            _service = new ImageImportService(NullLogger<ImageImportService>.Instance);
            await Task.CompletedTask;
        }

        [After(Test)]
        public async Task Cleanup()
        {
            TestHelpers.CleanupTempDirectory(_tempDir);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates a real JPEG image in memory using ImageSharp.
        /// </summary>
        private static MemoryStream CreateTestImage(int width = 100, int height = 100)
        {
            var image = new Image<Rgba32>(width, height, Color.Blue);
            var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            ms.Position = 0;
            return ms;
        }

        #region ProcessImageAsync Tests

        [Test]
        public async Task ProcessImageAsync_SmallImage_DoesNotResize()
        {
            using var stream = CreateTestImage(100, 100);
            var targetPath = Path.Combine(_tempDir, "output.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 200);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.WasResized).IsFalse();
            await Assert.That(result.Width).IsEqualTo(100);
            await Assert.That(result.Height).IsEqualTo(100);
            await Assert.That(result.OriginalWidth).IsEqualTo(100);
            await Assert.That(result.OriginalHeight).IsEqualTo(100);
            await Assert.That(result.FileSize).IsGreaterThan(0);
            await Assert.That(File.Exists(targetPath)).IsTrue();
        }

        [Test]
        public async Task ProcessImageAsync_LargeImage_ResizesWhenExceedsMaxDimension()
        {
            using var stream = CreateTestImage(2000, 1000);
            var targetPath = Path.Combine(_tempDir, "output.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 800);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.WasResized).IsTrue();
            await Assert.That(result.Width).IsEqualTo(800);
            await Assert.That(result.Height).IsEqualTo(400);
            await Assert.That(result.OriginalWidth).IsEqualTo(2000);
            await Assert.That(result.OriginalHeight).IsEqualTo(1000);
            await Assert.That(result.FileSize).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessImageAsync_CreatesTargetDirectory_IfNotExists()
        {
            using var stream = CreateTestImage(100, 100);
            var nestedDir = Path.Combine(_tempDir, "subdir", "nested");
            var targetPath = Path.Combine(nestedDir, "output.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 200);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(targetPath)).IsTrue();
            await Assert.That(Directory.Exists(nestedDir)).IsTrue();
        }

        [Test]
        public async Task ProcessImageAsync_InvalidStream_ReturnsFailedResult()
        {
            using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });
            var targetPath = Path.Combine(_tempDir, "output.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 800);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.ErrorMessage).IsNotNull();
        }

        [Test]
        public async Task ProcessImageAsync_LandscapeImage_PreservesAspectRatio()
        {
            // Landscape: width > height (2000x1000, ratio 2:1)
            using var stream = CreateTestImage(2000, 1000);
            var targetPath = Path.Combine(_tempDir, "landscape.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 800);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.WasResized).IsTrue();
            await Assert.That(result.Width).IsEqualTo(800);
            await Assert.That(result.Height).IsEqualTo(400);

            // Verify aspect ratio is preserved: 2000/1000 = 800/400 = 2.0
            var originalRatio = (double)result.OriginalWidth / result.OriginalHeight;
            var newRatio = (double)result.Width / result.Height;
            await Assert.That(Math.Abs(originalRatio - newRatio)).IsLessThan(0.01);
        }

        [Test]
        public async Task ProcessImageAsync_PortraitImage_PreservesAspectRatio()
        {
            // Portrait: height > width (1000x2000, ratio 1:2)
            using var stream = CreateTestImage(1000, 2000);
            var targetPath = Path.Combine(_tempDir, "portrait.jpg");

            var result = await _service.ProcessImageAsync(stream, targetPath, 800);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.WasResized).IsTrue();
            await Assert.That(result.Width).IsEqualTo(400);
            await Assert.That(result.Height).IsEqualTo(800);

            // Verify aspect ratio is preserved: 1000/2000 = 400/800 = 0.5
            var originalRatio = (double)result.OriginalWidth / result.OriginalHeight;
            var newRatio = (double)result.Width / result.Height;
            await Assert.That(Math.Abs(originalRatio - newRatio)).IsLessThan(0.01);
        }

        #endregion

        #region ExtractMetadataAsync Tests

        [Test]
        public async Task ExtractMetadataAsync_ReturnsWidthAndHeight()
        {
            using var stream = CreateTestImage(640, 480);

            var metadata = await _service.ExtractMetadataAsync(stream);

            await Assert.That(metadata).IsNotNull();
            await Assert.That(metadata!.Width).IsEqualTo(640);
            await Assert.That(metadata.Height).IsEqualTo(480);
        }

        [Test]
        public async Task ExtractMetadataAsync_InvalidStream_ReturnsNull()
        {
            using var stream = new MemoryStream(new byte[] { 0xFF, 0x00, 0xAB });

            var metadata = await _service.ExtractMetadataAsync(stream);

            await Assert.That(metadata).IsNull();
        }

        #endregion
    }
}
