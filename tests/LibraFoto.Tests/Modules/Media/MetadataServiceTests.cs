using LibraFoto.Modules.Media.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media
{
    /// <summary>
    /// Comprehensive unit tests for MetadataService covering EXIF data extraction.
    /// Tests all metadata extraction scenarios including GPS, camera settings, dates, dimensions, and error handling.
    /// </summary>
    public class MetadataServiceTests
    {
        private MetadataService _service = null!;
        private string _tempDir = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _service = new MetadataService();
            _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoMetadataTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            await Task.CompletedTask;
        }

        [After(Test)]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        // ─── Basic EXIF Extraction Tests ────────────────────────────────────────

        [Test]
        public async Task ExtractMetadata_WithCompleteExifData_ExtractsAllFields()
        {
            // Arrange
            var dateTaken = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Local);
            var imagePath = await CreateImageWithExifAsync("complete_exif.jpg", exif =>
            {
                exif.SetValue(ExifTag.Make, "Canon");
                exif.SetValue(ExifTag.Model, "EOS R5");
                exif.SetValue(ExifTag.LensModel, "RF 24-70mm F2.8L IS USM");
                exif.SetValue(ExifTag.DateTimeOriginal, dateTaken.ToString("yyyy:MM:dd HH:mm:ss"));
                exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 800 });
                exif.SetValue(ExifTag.FNumber, new Rational(28, 10)); // f/2.8
                exif.SetValue(ExifTag.ExposureTime, new Rational(1, 1000)); // 1/1000s
                exif.SetValue(ExifTag.FocalLength, new Rational(50, 1)); // 50mm
                exif.SetValue(ExifTag.Orientation, (ushort)1);
                exif.SetValue(ExifTag.ColorSpace, (ushort)1); // sRGB
                exif.SetValue(ExifTag.PixelXDimension, 800);
                exif.SetValue(ExifTag.PixelYDimension, 600);
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraMake).IsEqualTo("Canon");
            await Assert.That(result.CameraModel).IsEqualTo("EOS R5");
            await Assert.That(result.LensModel).IsEqualTo("RF 24-70mm F2.8L IS USM");
            await Assert.That(result.DateTaken).IsNotNull();
            await Assert.That(result.DateTaken!.Value.Year).IsEqualTo(2024);
            await Assert.That(result.DateTaken!.Value.Month).IsEqualTo(6);
            await Assert.That(result.DateTaken!.Value.Day).IsEqualTo(15);
            await Assert.That(result.Iso).IsEqualTo(800);
            await Assert.That(result.Aperture).IsNotNull();
            await Assert.That(Math.Abs(result.Aperture!.Value - 2.8)).IsLessThan(0.1);
            await Assert.That(result.FocalLength).IsNotNull();
            await Assert.That(Math.Abs(result.FocalLength!.Value - 50.0)).IsLessThan(0.1);
            await Assert.That(result.Orientation).IsEqualTo(1);
            await Assert.That(result.Width).IsEqualTo(800);
            await Assert.That(result.Height).IsEqualTo(600);
        }

        [Test]
        public async Task ExtractMetadata_FromFilePath_ReturnsSuccessfulMetadata()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("filepath_test.jpg", 100, 100);

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        [Test]
        public async Task ExtractMetadata_FromStream_ReturnsSuccessfulMetadata()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("stream_test.jpg", 100, 100);
            await using var stream = File.OpenRead(imagePath);

            // Act
            var result = _service.ExtractMetadata(stream, "stream_test.jpg");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        // ─── Date Extraction Tests ──────────────────────────────────────────────

        [Test]
        public async Task ExtractDateTaken_WithDateTimeOriginal_ReturnsCorrectDate()
        {
            // Arrange
            var expectedDate = new DateTime(2023, 12, 25, 10, 30, 0, DateTimeKind.Local);
            var imagePath = await CreateImageWithExifAsync("date_original.jpg", exif =>
            {
                exif.SetValue(ExifTag.DateTimeOriginal, expectedDate.ToString("yyyy:MM:dd HH:mm:ss"));
            });

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractDateTaken(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Year).IsEqualTo(2023);
            await Assert.That(result!.Value.Month).IsEqualTo(12);
            await Assert.That(result!.Value.Day).IsEqualTo(25);
        }

        [Test]
        public async Task ExtractDateTaken_WithDateTimeDigitized_ReturnsDate()
        {
            // Arrange
            var expectedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
            var imagePath = await CreateImageWithExifAsync("date_digitized.jpg", exif =>
            {
                exif.SetValue(ExifTag.DateTimeDigitized, expectedDate.ToString("yyyy:MM:dd HH:mm:ss"));
            });

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractDateTaken(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Year).IsEqualTo(2024);
        }

        [Test]
        public async Task ExtractDateTaken_WithoutExifDate_ReturnsNull()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("no_date.jpg", 100, 100);

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractDateTaken(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        // ─── GPS Coordinates Extraction Tests ───────────────────────────────────

        [Test]
        public async Task ExtractGpsCoordinates_WithValidGpsData_ReturnsCoordinates()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("with_gps.jpg", exif =>
            {
                // San Francisco coordinates: 37.7749° N, 122.4194° W
                exif.SetValue(ExifTag.GPSLatitude, new[] {
                    new Rational(37, 1),
                    new Rational(46, 1),
                    new Rational(2964, 100)
                });
                exif.SetValue(ExifTag.GPSLatitudeRef, "N");
                exif.SetValue(ExifTag.GPSLongitude, new[] {
                    new Rational(122, 1),
                    new Rational(25, 1),
                    new Rational(1584, 100)
                });
                exif.SetValue(ExifTag.GPSLongitudeRef, "W");
                exif.SetValue(ExifTag.GPSAltitude, new Rational(50, 1)); // 50m
                exif.SetValue(ExifTag.GPSAltitudeRef, (byte)0);
            });

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractGpsCoordinates(stream);

            // Assert - Allow some tolerance for rational calculation precision
            await Assert.That(result).IsNotNull();
            await Assert.That(Math.Abs(result!.Value.Latitude - 37.7749)).IsLessThan(0.01);
            await Assert.That(Math.Abs(result!.Value.Longitude - (-122.4194))).IsLessThan(0.01);
        }

        [Test]
        public async Task ExtractGpsCoordinates_WithoutGpsData_ReturnsNull()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("no_gps.jpg", 100, 100);

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractGpsCoordinates(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task ExtractMetadata_WithGpsAltitude_ExtractsAltitude()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("with_altitude.jpg", exif =>
            {
                exif.SetValue(ExifTag.GPSLatitude, new[] {
                    new Rational(40, 1), new Rational(0, 1), new Rational(0, 1)
                });
                exif.SetValue<string>(ExifTag.GPSLatitudeRef, "N");
                exif.SetValue(ExifTag.GPSLongitude, new[] {
                    new Rational(122, 1), new Rational(25, 1), new Rational(1584, 100)
                });
                exif.SetValue<string>(ExifTag.GPSLongitudeRef, "W");
                exif.SetValue(ExifTag.GPSAltitude, new Rational(1500, 1)); // 1500m
                exif.SetValue<byte>(ExifTag.GPSAltitudeRef, 0);
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Altitude).IsNotNull();
            await Assert.That(Math.Abs(result.Altitude!.Value - 1500.0)).IsLessThan(0.1);
        }

        // ─── Camera Settings Extraction Tests ───────────────────────────────────

        [Test]
        public async Task ExtractMetadata_WithCameraSettings_ExtractsIsoApertureFocalLength()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("camera_settings.jpg", exif =>
            {
                exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 1600 });
                exif.SetValue(ExifTag.FNumber, new Rational(56, 10)); // f/5.6
                exif.SetValue(ExifTag.FocalLength, new Rational(200, 1)); // 200mm
                exif.SetValue(ExifTag.ExposureTime, new Rational(1, 500)); // 1/500s
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Iso).IsEqualTo(1600);
            await Assert.That(result.Aperture).IsNotNull();
            await Assert.That(Math.Abs(result.Aperture!.Value - 5.6)).IsLessThan(0.1);
            await Assert.That(result.FocalLength).IsNotNull();
            await Assert.That(Math.Abs(result.FocalLength!.Value - 200.0)).IsLessThan(0.1);
            await Assert.That(result.ShutterSpeedFormatted).IsNotNull();
        }

        [Test]
        public async Task ExtractMetadata_WithHighIso_ExtractsCorrectValue()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("high_iso.jpg", exif =>
            {
                exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 6400 });
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Iso).IsEqualTo(6400);
        }

        [Test]
        public async Task ExtractMetadata_WithWideFStop_ExtractsCorrectAperture()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("wide_aperture.jpg", exif =>
            {
                exif.SetValue(ExifTag.FNumber, new Rational(14, 10)); // f/1.4
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Aperture).IsNotNull();
            await Assert.That(Math.Abs(result.Aperture!.Value - 1.4)).IsLessThan(0.1);
        }

        // ─── Orientation Handling Tests ─────────────────────────────────────────

        [Test]
        [Arguments(1, "Normal")]
        [Arguments(3, "Rotate 180")]
        [Arguments(6, "Rotate 90 CW")]
        [Arguments(8, "Rotate 270 CW")]
        public async Task ExtractMetadata_WithOrientation_ExtractsCorrectValue(int orientationValue, string description)
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync($"orientation_{orientationValue}.jpg", exif =>
            {
                exif.SetValue(ExifTag.Orientation, (ushort)orientationValue);
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Orientation).IsEqualTo(orientationValue);
        }

        // ─── Dimensions Extraction Tests ────────────────────────────────────────

        [Test]
        public async Task ExtractDimensions_WithExifDimensions_ReturnsCorrectSize()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("dimensions_1920x1080.jpg", exif =>
            {
                exif.SetValue(ExifTag.PixelXDimension, 1920);
                exif.SetValue(ExifTag.PixelYDimension, 1080);
            }, 1920, 1080);

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractDimensions(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Width).IsEqualTo(1920);
            await Assert.That(result!.Value.Height).IsEqualTo(1080);
        }

        [Test]
        public async Task ExtractDimensions_WithLargeImage_ReturnsCorrectDimensions()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("large_image.jpg", 4000, 3000);

            // Act
            await using var stream = File.OpenRead(imagePath);
            var result = _service.ExtractDimensions(stream);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Width).IsEqualTo(4000);
            await Assert.That(result!.Value.Height).IsEqualTo(3000);
        }

        // ─── Multiple Image Formats Tests ───────────────────────────────────────

        [Test]
        public async Task ExtractMetadata_FromJpegImage_ExtractsMetadata()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("test.jpg", exif =>
            {
                exif.SetValue(ExifTag.Make, "Sony");
                exif.SetValue(ExifTag.Model, "A7 III");
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraMake).IsEqualTo("Sony");
        }

        [Test]
        public async Task ExtractMetadata_FromPngImage_HandlesGracefully()
        {
            // Arrange - PNG with minimal EXIF support
            var imagePath = Path.Combine(_tempDir, "test.png");
            using (var image = new Image<Rgba32>(200, 200, Color.Green))
            {
                await image.SaveAsync(imagePath, new PngEncoder());
            }

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert - PNG typically has limited EXIF, but should not fail
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        // ─── Edge Cases and Error Handling Tests ────────────────────────────────

        [Test]
        public async Task ExtractMetadata_WithNoExifData_ReturnsSuccessWithoutMetadata()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("no_exif.jpg", 100, 100);

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraMake).IsNull();
            await Assert.That(result.CameraModel).IsNull();
            await Assert.That(result.DateTaken).IsNull();
        }

        [Test]
        public async Task ExtractMetadata_WithPartialExifData_ExtractsAvailableFields()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("partial_exif.jpg", exif =>
            {
                exif.SetValue(ExifTag.Make, "Nikon");
                // Intentionally omit Model, DateTaken, and other fields
            });

            // Act
            var result = _service.ExtractMetadata(imagePath);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraMake).IsEqualTo("Nikon");
            await Assert.That(result.CameraModel).IsNull();
            await Assert.That(result.DateTaken).IsNull();
        }

        [Test]
        public async Task ExtractMetadata_WithInvalidFilePath_ReturnsUnsuccessfulResult()
        {
            // Arrange
            var invalidPath = Path.Combine(_tempDir, "nonexistent_file.jpg");

            // Act
            var result = _service.ExtractMetadata(invalidPath);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsFalse();
        }

        [Test]
        public async Task ExtractMetadata_WithCorruptedStream_ReturnsUnsuccessfulResult()
        {
            // Arrange - Invalid image data
            await using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0x00, 0x01, 0x02 });

            // Act
            var result = _service.ExtractMetadata(stream, "corrupted.jpg");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsFalse();
        }

        [Test]
        public async Task ExtractMetadata_WithEmptyStream_ReturnsUnsuccessfulResult()
        {
            // Arrange
            await using var stream = new MemoryStream();

            // Act
            var result = _service.ExtractMetadata(stream, "empty.jpg");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsFalse();
        }

        [Test]
        public async Task ExtractDateTaken_WithCorruptedFile_ReturnsNull()
        {
            // Arrange
            await using var stream = new MemoryStream(new byte[] { 0, 1, 2, 3, 4, 5 });

            // Act
            var result = _service.ExtractDateTaken(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task ExtractGpsCoordinates_WithCorruptedFile_ReturnsNull()
        {
            // Arrange
            await using var stream = new MemoryStream(new byte[] { 0xFF, 0x00 });

            // Act
            var result = _service.ExtractGpsCoordinates(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task ExtractDimensions_WithCorruptedFile_ReturnsNull()
        {
            // Arrange
            await using var stream = new MemoryStream(new byte[] { 0xAA, 0xBB });

            // Act
            var result = _service.ExtractDimensions(stream);

            // Assert
            await Assert.That(result).IsNull();
        }

        // ─── Async Method Tests ─────────────────────────────────────────────────

        [Test]
        [NotInParallel]
        public async Task ExtractMetadataAsync_FromFilePath_ReturnsMetadata()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("async_file.jpg", exif =>
            {
                exif.SetValue(ExifTag.Make, "Fujifilm");
            });

            // Act
            var result = await _service.ExtractMetadataAsync(imagePath, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraMake).IsEqualTo("Fujifilm");
        }

        [Test]
        [NotInParallel]
        public async Task ExtractMetadataAsync_FromStream_ReturnsMetadata()
        {
            // Arrange
            var imagePath = await CreateImageWithExifAsync("async_stream.jpg", exif =>
            {
                exif.SetValue(ExifTag.Model, "X-T5");
            });
            await using var stream = File.OpenRead(imagePath);

            // Act
            var result = await _service.ExtractMetadataAsync(stream, "async_stream.jpg", CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.CameraModel).IsEqualTo("X-T5");
        }

        [Test]
        [NotInParallel]
        public async Task ExtractMetadataAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var imagePath = await CreateSimpleImageAsync("cancellation_test.jpg", 100, 100);
            using var cts = new CancellationTokenSource();

            // Act
            var result = await _service.ExtractMetadataAsync(imagePath, cts.Token);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Success).IsTrue();
        }

        // ─── Helper Methods ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a simple test image without EXIF data.
        /// </summary>
        private async Task<string> CreateSimpleImageAsync(string filename, int width, int height)
        {
            var path = Path.Combine(_tempDir, filename);
            using var image = new Image<Rgba32>(width, height, Color.Blue);
            await image.SaveAsync(path, new JpegEncoder());
            return path;
        }

        /// <summary>
        /// Creates a test image with specified EXIF metadata.
        /// </summary>
        private async Task<string> CreateImageWithExifAsync(
            string filename,
            Action<ExifProfile> configureExif,
            int width = 800,
            int height = 600)
        {
            var path = Path.Combine(_tempDir, filename);
            using var image = new Image<Rgba32>(width, height, Color.Red);

            var exifProfile = new ExifProfile();
            configureExif(exifProfile);
            image.Metadata.ExifProfile = exifProfile;

            await image.SaveAsync(path, new JpegEncoder { Quality = 90 });
            return path;
        }
    }
}
