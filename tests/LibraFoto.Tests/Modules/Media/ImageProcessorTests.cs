using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    public class ImageProcessorTests
    {
        private ImageProcessor _processor = null!;
        private readonly List<string> _tempFiles = new();

        [Before(Test)]
        public async Task Setup()
        {
            _processor = new ImageProcessor();
            await Task.CompletedTask;
        }

        [After(Test)]
        public async Task Cleanup()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _tempFiles.Clear();
            await Task.CompletedTask;
        }

        private static MemoryStream CreateTestImage(int width = 100, int height = 100)
        {
            var image = new Image<Rgba32>(width, height, Color.Red);
            var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateInvalidStream()
        {
            return new MemoryStream(new byte[] { 0, 0, 0, 0 });
        }

        private string CreateTempFilePath(string extension = ".jpg")
        {
            var path = Path.Combine(Path.GetTempPath(), $"LibraFotoTest_{Guid.NewGuid()}{extension}");
            _tempFiles.Add(path);
            return path;
        }

        private async Task<string> CreateTempImageFile(int width = 100, int height = 100)
        {
            var path = CreateTempFilePath();
            using var image = new Image<Rgba32>(width, height, Color.Red);
            await image.SaveAsJpegAsync(path);
            return path;
        }

        // ─── ProcessAsync (stream overload) ─────────────────────────────

        [Test]
        public async Task ProcessAsync_WithValidImage_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_WithResizeOptions_OutputIsSmaller()
        {
            // Arrange
            using var source = CreateTestImage(200, 200);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { MaxDimension = 50 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(50);
            await Assert.That(dimensions!.Value.Height).IsEqualTo(50);
        }

        [Test]
        public async Task ProcessAsync_WithRotation_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions { RotationDegrees = 90 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_WithFlipHorizontal_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions { FlipHorizontal = true };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_WithInvalidStream_ReturnsFalse()
        {
            // Arrange
            using var source = CreateInvalidStream();
            using var output = new MemoryStream();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── ProcessAsync (file path overload) ──────────────────────────

        [Test]
        [NotInParallel]
        public async Task ProcessAsync_FilePath_WithValidImage_ReturnsTrue()
        {
            // Arrange
            var sourcePath = await CreateTempImageFile();
            var outputPath = CreateTempFilePath();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessAsync(sourcePath, outputPath, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(File.Exists(outputPath)).IsTrue();
            await Assert.That(new FileInfo(outputPath).Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_FilePath_WithNonexistentSource_ReturnsFalse()
        {
            // Arrange
            var sourcePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.jpg");
            var outputPath = CreateTempFilePath();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessAsync(sourcePath, outputPath, options);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── ProcessToBytesAsync ─────────────────────────────────────────

        [Test]
        public async Task ProcessToBytesAsync_WithValidImage_ReturnsBytes()
        {
            // Arrange
            using var source = CreateTestImage();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessToBytesAsync(source, options);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessToBytesAsync_WithInvalidStream_ReturnsNull()
        {
            // Arrange
            using var source = CreateInvalidStream();
            var options = new ProcessingOptions();

            // Act
            var result = await _processor.ProcessToBytesAsync(source, options);

            // Assert
            await Assert.That(result).IsNull();
        }

        // ─── ResizeAsync ─────────────────────────────────────────────────

        [Test]
        public async Task ResizeAsync_WithValidImage_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage(200, 200);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ResizeAsync(source, output, 50, 50);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(50);
            await Assert.That(dimensions!.Value.Height).IsEqualTo(50);
        }

        [Test]
        public async Task ResizeAsync_WithInvalidStream_ReturnsFalse()
        {
            // Arrange
            using var source = CreateInvalidStream();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ResizeAsync(source, output, 50, 50);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── RotateAsync ─────────────────────────────────────────────────

        [Test]
        public async Task RotateAsync_WithValidImage_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.RotateAsync(source, output, 90);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task RotateAsync_WithInvalidStream_ReturnsFalse()
        {
            // Arrange
            using var source = CreateInvalidStream();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.RotateAsync(source, output, 90);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── ConvertAsync ────────────────────────────────────────────────

        [Test]
        public async Task ConvertAsync_ToJpeg_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.Jpeg);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ConvertAsync_ToPng_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.Png);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ConvertAsync_ToWebP_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.WebP);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ConvertAsync_WithInvalidStream_ReturnsFalse()
        {
            // Arrange
            using var source = CreateInvalidStream();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.Jpeg);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── AutoOrientAsync ─────────────────────────────────────────────

        [Test]
        public async Task AutoOrientAsync_WithValidImage_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.AutoOrientAsync(source, output);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task AutoOrientAsync_WithInvalidStream_ReturnsFalse()
        {
            // Arrange
            using var source = CreateInvalidStream();
            using var output = new MemoryStream();

            // Act
            var result = await _processor.AutoOrientAsync(source, output);

            // Assert
            await Assert.That(result).IsFalse();
        }

        // ─── GetDimensions ───────────────────────────────────────────────

        [Test]
        public async Task GetDimensions_WithValidImage_ReturnsDimensions()
        {
            // Arrange
            using var source = CreateTestImage(150, 75);

            // Act
            var result = _processor.GetDimensions(source);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Width).IsEqualTo(150);
            await Assert.That(result!.Value.Height).IsEqualTo(75);
        }

        [Test]
        public async Task GetDimensions_WithInvalidStream_ReturnsNull()
        {
            // Arrange
            using var source = CreateInvalidStream();

            // Act
            var result = _processor.GetDimensions(source);

            // Assert
            await Assert.That(result).IsNull();
        }

        // ─── IsSupportedFormat ───────────────────────────────────────────

        [Test]
        public async Task IsSupportedFormat_Jpg_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("jpg");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Jpeg_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("jpeg");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Png_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("png");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Webp_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("webp");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Gif_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("gif");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Bmp_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("bmp");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Tiff_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat("tiff");
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_Exe_ReturnsFalse()
        {
            var result = _processor.IsSupportedFormat("exe");
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task IsSupportedFormat_WithDot_ReturnsTrue()
        {
            var result = _processor.IsSupportedFormat(".jpg");
            await Assert.That(result).IsTrue();
        }

        // ─── GetExtension ────────────────────────────────────────────────

        [Test]
        public async Task GetExtension_Jpeg_ReturnsJpg()
        {
            var result = _processor.GetExtension(ImageOutputFormat.Jpeg);
            await Assert.That(result).IsEqualTo("jpg");
        }

        [Test]
        public async Task GetExtension_Png_ReturnsPng()
        {
            var result = _processor.GetExtension(ImageOutputFormat.Png);
            await Assert.That(result).IsEqualTo("png");
        }

        [Test]
        public async Task GetExtension_WebP_ReturnsWebp()
        {
            var result = _processor.GetExtension(ImageOutputFormat.WebP);
            await Assert.That(result).IsEqualTo("webp");
        }

        // ─── GetContentType ─────────────────────────────────────────────

        [Test]
        public async Task GetContentType_Jpeg_ReturnsCorrectMime()
        {
            var result = _processor.GetContentType(ImageOutputFormat.Jpeg);
            await Assert.That(result).IsEqualTo("image/jpeg");
        }

        [Test]
        public async Task GetContentType_Png_ReturnsCorrectMime()
        {
            var result = _processor.GetContentType(ImageOutputFormat.Png);
            await Assert.That(result).IsEqualTo("image/png");
        }

        [Test]
        public async Task GetContentType_WebP_ReturnsCorrectMime()
        {
            var result = _processor.GetContentType(ImageOutputFormat.WebP);
            await Assert.That(result).IsEqualTo("image/webp");
        }
    }
}
