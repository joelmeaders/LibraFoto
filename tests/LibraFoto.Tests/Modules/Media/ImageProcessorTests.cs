using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    [NotInParallel]
    public class ImageProcessorTests
    {
        private ImageProcessor _processor = null!;
        private readonly List<string> _tempFiles = new();
        private string _tempDir = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _processor = new ImageProcessor();
            _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoImageProcessorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            await Task.CompletedTask;
        }

        [After(Test)]
        public async Task Cleanup()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try
                    { File.Delete(file); }
                    catch { }
                }
            }

            _tempFiles.Clear();

            if (Directory.Exists(_tempDir))
            {
                try
                { Directory.Delete(_tempDir, true); }
                catch { }
            }

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
            var path = Path.Combine(_tempDir, $"test_{Guid.NewGuid()}{extension}");
            _tempFiles.Add(path);
            return path;
        }

        private async Task<string> CreateTempImageFile(int width = 100, int height = 100, bool isPng = false)
        {
            var path = CreateTempFilePath(isPng ? ".png" : ".jpg");
            using var image = new Image<Rgba32>(width, height, Color.Red);
            if (isPng)
            {
                await image.SaveAsync(path, new PngEncoder());
            }
            else
            {
                await image.SaveAsync(path, new JpegEncoder { Quality = 90 });
            }
            return path;
        }

        private async Task<string> CreateGradientImageFile(int width, int height)
        {
            var path = CreateTempFilePath();
            using var image = new Image<Rgba32>(width, height);

            // Create a gradient for visual testing
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var r = (byte)(255 * x / width);
                    var g = (byte)(255 * y / height);
                    var b = (byte)(128);
                    image[x, y] = new Rgba32(r, g, b);
                }
            }

            await image.SaveAsync(path, new JpegEncoder { Quality = 90 });
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
        public async Task ProcessAsync_WithMaxDimension_ResizesCorrectly()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { MaxDimension = 400 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(400);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(400);
        }

        [Test]
        public async Task ProcessAsync_WithExactWidthAndHeight_ResizesToExactDimensions()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = new ProcessingOptions
            {
                Width = 200,
                Height = 200,
                ResizeMode = ImageResizeMode.Stretch
            };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(200);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        public async Task ProcessAsync_WithWidthOnly_PreservesAspectRatio()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { Width = 400 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(400);
            await Assert.That(dimensions.Value.Height).IsEqualTo(300); // 400 * 600/800
        }

        [Test]
        public async Task ProcessAsync_WithHeightOnly_PreservesAspectRatio()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { Height = 300 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(400); // 300 * 800/600
            await Assert.That(dimensions.Value.Height).IsEqualTo(300);
        }

        [Test]
        public async Task ProcessAsync_WithRotation90_SwapsDimensions()
        {
            // Arrange
            using var source = CreateTestImage(200, 100);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { RotationDegrees = 90 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(100);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        public async Task ProcessAsync_WithRotation180_PreservesDimensions()
        {
            // Arrange
            using var source = CreateTestImage(300, 200);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { RotationDegrees = 180 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        public async Task ProcessAsync_WithRotation270_SwapsDimensions()
        {
            // Arrange
            using var source = CreateTestImage(500, 300);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { RotationDegrees = 270 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
            await Assert.That(dimensions.Value.Height).IsEqualTo(500);
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
        public async Task ProcessAsync_WithFlipVertical_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions { FlipVertical = true };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_WithBothFlips_ReturnsTrue()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions
            {
                FlipHorizontal = true,
                FlipVertical = true
            };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ProcessAsync_WithOutputFormat_ConvertsFormat()
        {
            // Arrange
            using var source = CreateTestImage();
            using var output = new MemoryStream();
            var options = new ProcessingOptions { OutputFormat = ImageOutputFormat.Png };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            using var image = await Image.LoadAsync(output);
            await Assert.That(image.Metadata.DecodedImageFormat!.Name).IsEqualTo("PNG");
        }

        [Test]
        public async Task ProcessAsync_CombinedOperations_AppliesAll()
        {
            // Arrange
            using var source = CreateTestImage(1000, 800);
            using var output = new MemoryStream();
            var options = new ProcessingOptions
            {
                MaxDimension = 400,
                RotationDegrees = 90,
                FlipHorizontal = true,
                OutputFormat = ImageOutputFormat.Png,
                AutoOrient = true
            };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(400);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(400);
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
        [NotInParallel]
        public async Task ProcessAsync_FilePath_WithRotation_RotatesCorrectly()
        {
            // Arrange
            var sourcePath = await CreateTempImageFile(200, 100);
            var outputPath = CreateTempFilePath();
            var options = new ProcessingOptions { RotationDegrees = 90 };

            // Act
            var result = await _processor.ProcessAsync(sourcePath, outputPath, options);

            // Assert
            await Assert.That(result).IsTrue();
            await using var stream = File.OpenRead(outputPath);
            var dimensions = _processor.GetDimensions(stream);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(100);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        public async Task ProcessAsync_FilePath_WithNonexistentSource_ReturnsFalse()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempDir, $"nonexistent_{Guid.NewGuid()}.jpg");
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
            using var source = CreateTestImage(400, 300);
            var options = new ProcessingOptions { MaxDimension = 200 };

            // Act
            var result = await _processor.ProcessToBytesAsync(source, options);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Length).IsGreaterThan(0);

            // Verify can load as image
            using var resultImage = Image.Load(result);
            await Assert.That(resultImage.Width).IsLessThanOrEqualTo(200);
            await Assert.That(resultImage.Height).IsLessThanOrEqualTo(200);
        }

        [Test]
        public async Task ProcessToBytesAsync_WithWebPFormat_ReturnsWebPBytes()
        {
            // Arrange
            using var source = CreateTestImage(200, 200);
            var options = new ProcessingOptions
            {
                OutputFormat = ImageOutputFormat.WebP,
                WebPQuality = 80
            };

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
        public async Task ResizeAsync_PreservesAspectRatio()
        {
            // Arrange
            using var source = CreateTestImage(1600, 1200);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ResizeAsync(source, output, 800, 600);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(800);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(600);
        }

        [Test]
        public async Task ResizeAsync_WithVerySmallDimensions_Works()
        {
            // Arrange
            using var source = CreateTestImage(2000, 1500);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ResizeAsync(source, output, 10, 10);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(10);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(10);
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
        public async Task RotateAsync_By90Degrees_SwapsDimensions()
        {
            // Arrange
            using var source = CreateTestImage(400, 200);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.RotateAsync(source, output, 90);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(200);
            await Assert.That(dimensions.Value.Height).IsEqualTo(400);
        }

        [Test]
        public async Task RotateAsync_By180Degrees_PreservesDimensions()
        {
            // Arrange
            using var source = CreateTestImage(300, 200);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.RotateAsync(source, output, 180);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        public async Task RotateAsync_By270Degrees_SwapsDimensions()
        {
            // Arrange
            using var source = CreateTestImage(500, 300);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.RotateAsync(source, output, 270);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
            await Assert.That(dimensions.Value.Height).IsEqualTo(500);
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
        public async Task ConvertAsync_ToJpeg_CreatesJpeg()
        {
            // Arrange
            var sourcePath = await CreateTempImageFile(200, 200, isPng: true);
            using var source = File.OpenRead(sourcePath);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.Jpeg, 85);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            using var image = await Image.LoadAsync(output);
            await Assert.That(image.Metadata.DecodedImageFormat!.Name).IsEqualTo("JPEG");
        }

        [Test]
        public async Task ConvertAsync_ToPng_CreatesPng()
        {
            // Arrange
            using var source = CreateTestImage(150, 150);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.Png);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            using var image = await Image.LoadAsync(output);
            await Assert.That(image.Metadata.DecodedImageFormat!.Name).IsEqualTo("PNG");
        }

        [Test]
        public async Task ConvertAsync_ToWebP_CreatesWebP()
        {
            // Arrange
            using var source = CreateTestImage(200, 200);
            using var output = new MemoryStream();

            // Act
            var result = await _processor.ConvertAsync(source, output, ImageOutputFormat.WebP, 80);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ConvertAsync_WithDifferentQualityLevels_AffectsFileSize()
        {
            // Arrange
            using var source1 = CreateTestImage(400, 400);
            using var source2 = CreateTestImage(400, 400);

            // Act - High quality
            using var highQualityStream = new MemoryStream();
            await _processor.ConvertAsync(source1, highQualityStream, ImageOutputFormat.Jpeg, 95);

            // Act - Low quality
            using var lowQualityStream = new MemoryStream();
            await _processor.ConvertAsync(source2, lowQualityStream, ImageOutputFormat.Jpeg, 50);

            // Assert - High quality should be larger
            await Assert.That(highQualityStream.Length).IsGreaterThan(lowQualityStream.Length);
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
            using var source = CreateTestImage(200, 300);
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
            using var source = CreateTestImage(640, 480);

            // Act
            var result = _processor.GetDimensions(source);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Width).IsEqualTo(640);
            await Assert.That(result!.Value.Height).IsEqualTo(480);
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
        public async Task IsSupportedFormat_WithSupportedFormats_ReturnsTrue()
        {
            await Assert.That(_processor.IsSupportedFormat("jpg")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat(".jpg")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("jpeg")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat(".jpeg")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("png")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat(".png")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("gif")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("webp")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("bmp")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("tiff")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("tif")).IsTrue();
        }

        [Test]
        public async Task IsSupportedFormat_WithUnsupportedFormats_ReturnsFalse()
        {
            await Assert.That(_processor.IsSupportedFormat("txt")).IsFalse();
            await Assert.That(_processor.IsSupportedFormat("pdf")).IsFalse();
            await Assert.That(_processor.IsSupportedFormat("doc")).IsFalse();
            await Assert.That(_processor.IsSupportedFormat("svg")).IsFalse();
            await Assert.That(_processor.IsSupportedFormat("exe")).IsFalse();
        }

        [Test]
        public async Task IsSupportedFormat_CaseInsensitive_ReturnsTrue()
        {
            await Assert.That(_processor.IsSupportedFormat("JPG")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("Png")).IsTrue();
            await Assert.That(_processor.IsSupportedFormat("JPEG")).IsTrue();
        }

        // ─── GetExtension ────────────────────────────────────────────────

        [Test]
        public async Task GetExtension_ReturnsCorrectExtensions()
        {
            await Assert.That(_processor.GetExtension(ImageOutputFormat.Jpeg)).IsEqualTo("jpg");
            await Assert.That(_processor.GetExtension(ImageOutputFormat.Png)).IsEqualTo("png");
            await Assert.That(_processor.GetExtension(ImageOutputFormat.WebP)).IsEqualTo("webp");
            await Assert.That(_processor.GetExtension(ImageOutputFormat.Gif)).IsEqualTo("gif");
            await Assert.That(_processor.GetExtension(ImageOutputFormat.Bmp)).IsEqualTo("bmp");
        }

        // ─── GetContentType ──────────────────────────────────────────────

        [Test]
        public async Task GetContentType_ReturnsCorrectMimeTypes()
        {
            await Assert.That(_processor.GetContentType(ImageOutputFormat.Jpeg)).IsEqualTo("image/jpeg");
            await Assert.That(_processor.GetContentType(ImageOutputFormat.Png)).IsEqualTo("image/png");
            await Assert.That(_processor.GetContentType(ImageOutputFormat.WebP)).IsEqualTo("image/webp");
            await Assert.That(_processor.GetContentType(ImageOutputFormat.Gif)).IsEqualTo("image/gif");
            await Assert.That(_processor.GetContentType(ImageOutputFormat.Bmp)).IsEqualTo("image/bmp");
        }

        // ─── Resize Mode Tests ───────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_WithMaxResizeMode_FitsWithinDimensions()
        {
            // Arrange
            using var source = CreateTestImage(1000, 500);
            using var output = new MemoryStream();
            var options = new ProcessingOptions
            {
                Width = 400,
                Height = 400,
                ResizeMode = ImageResizeMode.Max
            };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(400);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(400);
        }

        [Test]
        public async Task ProcessAsync_WithCropResizeMode_ExactDimensions()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = new ProcessingOptions
            {
                Width = 300,
                Height = 300,
                ResizeMode = ImageResizeMode.Crop
            };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
            await Assert.That(dimensions.Value.Height).IsEqualTo(300);
        }

        // ─── ProcessingOptions Presets Tests ─────────────────────────────

        [Test]
        public async Task ProcessAsync_WithThumbnailPreset_CreatesThumbnail()
        {
            // Arrange
            using var source = CreateTestImage(2000, 1500);
            using var output = new MemoryStream();
            var options = ProcessingOptions.ForThumbnail();

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(400);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(400);
        }

        [Test]
        public async Task ProcessAsync_WithWebDisplayPreset_CreatesWebSizedImage()
        {
            // Arrange
            using var source = CreateTestImage(3000, 2000);
            using var output = new MemoryStream();
            var options = ProcessingOptions.ForWebDisplay();

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(1920);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(1920);
        }

        [Test]
        public async Task ProcessAsync_WithWebDisplayPresetCustomMaxDimension_CreatesCorrectSize()
        {
            // Arrange
            using var source = CreateTestImage(2000, 1500);
            using var output = new MemoryStream();
            var options = ProcessingOptions.ForWebDisplay(1024);

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(1024);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(1024);
        }

        [Test]
        public async Task ProcessAsync_WithFullQualityPreset_PreservesQuality()
        {
            // Arrange
            using var source = CreateTestImage(800, 600);
            using var output = new MemoryStream();
            var options = ProcessingOptions.FullQuality();

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(output.Length).IsGreaterThan(0);
        }

        // ─── Edge Cases ──────────────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_WithVerySmallImage_Succeeds()
        {
            // Arrange
            using var source = CreateTestImage(10, 10);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { MaxDimension = 5 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task ProcessAsync_WithLargeImage_Succeeds()
        {
            // Arrange
            using var source = CreateTestImage(4000, 3000);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { MaxDimension = 800 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(800);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(800);
        }

        [Test]
        public async Task ProcessAsync_WithSquareImage_PreservesSquareAspect()
        {
            // Arrange
            using var source = CreateTestImage(500, 500);
            using var output = new MemoryStream();
            var options = new ProcessingOptions { MaxDimension = 200 };

            // Act
            var result = await _processor.ProcessAsync(source, output, options);

            // Assert
            await Assert.That(result).IsTrue();
            output.Position = 0;
            var dimensions = _processor.GetDimensions(output);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsEqualTo(200);
            await Assert.That(dimensions.Value.Height).IsEqualTo(200);
        }

        [Test]
        [NotInParallel]
        public async Task ProcessAsync_FilePath_WithComplexTransformations_AppliesAllOperations()
        {
            // Arrange
            var sourcePath = await CreateGradientImageFile(1200, 800);
            var outputPath = CreateTempFilePath();
            var options = new ProcessingOptions
            {
                MaxDimension = 500,
                RotationDegrees = 90,
                FlipHorizontal = true,
                OutputFormat = ImageOutputFormat.WebP,
                WebPQuality = 85
            };

            // Act
            var result = await _processor.ProcessAsync(sourcePath, outputPath, options);

            // Assert
            await Assert.That(result).IsTrue();
            await Assert.That(File.Exists(outputPath)).IsTrue();

            await using var stream = File.OpenRead(outputPath);
            var dimensions = _processor.GetDimensions(stream);
            await Assert.That(dimensions).IsNotNull();
            await Assert.That(dimensions!.Value.Width).IsLessThanOrEqualTo(500);
            await Assert.That(dimensions.Value.Height).IsLessThanOrEqualTo(500);
        }
    }
}
