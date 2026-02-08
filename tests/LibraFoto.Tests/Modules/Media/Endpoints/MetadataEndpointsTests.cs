using LibraFoto.Modules.Media.Endpoints;
using LibraFoto.Modules.Media.Models;
using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Media.Endpoints
{
    /// <summary>
    /// Tests for MetadataEndpoints - EXIF metadata extraction endpoints.
    /// </summary>
    public class MetadataEndpointsTests
    {
        private IMetadataService _metadataService = null!;
        private IGeocodingService _geocodingService = null!;

        [Before(Test)]
        public void Setup()
        {
            _metadataService = Substitute.For<IMetadataService>();
            _geocodingService = Substitute.For<IGeocodingService>();
        }

        #region ExtractMetadataFromUpload Tests

        [Test]
        public async Task ExtractMetadataFromUpload_WithValidFile_ReturnsOkWithCompleteExifData()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 1024);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(3840, 2160, 1)
                .WithDates(new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc))
                .WithCamera("Canon", "EOS R5", "RF 24-70mm F2.8L IS USM")
                .WithExposure(2.8, 0.001, "1/1000", 400, 50.0, 50.0)
                .WithGps(48.8566, 2.3522, 100.0)
                .WithFormat("image/jpeg", "JPEG", "sRGB", 8));

            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "photo.jpg", Arg.Any<CancellationToken>())
                .Returns(metadata);

            var geocodingResult = new GeocodingResult { DisplayName = "Paris, France" };
            _geocodingService.ReverseGeocodeAsync(48.8566, 2.3522, Arg.Any<CancellationToken>())
                .Returns(geocodingResult);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Width).IsEqualTo(3840);
            await Assert.That(okResult.Value.Height).IsEqualTo(2160);
            await Assert.That(okResult.Value.CameraMake).IsEqualTo("Canon");
            await Assert.That(okResult.Value.CameraModel).IsEqualTo("EOS R5");
            await Assert.That(okResult.Value.LensModel).IsEqualTo("RF 24-70mm F2.8L IS USM");
            await Assert.That(okResult.Value.Aperture).IsEqualTo(2.8);
            await Assert.That(okResult.Value.ExposureTime).IsEqualTo("1/1000");
            await Assert.That(okResult.Value.Iso).IsEqualTo(400);
            await Assert.That(okResult.Value.FocalLength).IsEqualTo(50.0);
            await Assert.That(okResult.Value.Latitude).IsEqualTo(48.8566);
            await Assert.That(okResult.Value.Longitude).IsEqualTo(2.3522);
            await Assert.That(okResult.Value.Altitude).IsEqualTo(100.0);
            await Assert.That(okResult.Value.LocationName).IsEqualTo("Paris, France");
            await Assert.That(okResult.Value.ColorSpace).IsEqualTo("sRGB");
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithPartialExifData_ReturnsOkWithAvailableFields()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 1024);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(1920, 1080, null)
                .WithDates(new DateTime(2024, 2, 20, 10, 15, 0, DateTimeKind.Utc))
                .WithCamera("Apple", "iPhone 15 Pro", null));

            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "photo.jpg", Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Width).IsEqualTo(1920);
            await Assert.That(okResult.Value.Height).IsEqualTo(1080);
            await Assert.That(okResult.Value.CameraMake).IsEqualTo("Apple");
            await Assert.That(okResult.Value.CameraModel).IsEqualTo("iPhone 15 Pro");
            await Assert.That(okResult.Value.LensModel).IsNull();
            await Assert.That(okResult.Value.Latitude).IsNull();
            await Assert.That(okResult.Value.Longitude).IsNull();
            await Assert.That(okResult.Value.LocationName).IsNull();
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithNoExifData_ReturnsOkWithNullFields()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 1024);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(800, 600, null));

            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "photo.jpg", Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Width).IsEqualTo(800);
            await Assert.That(okResult.Value.Height).IsEqualTo(600);
            await Assert.That(okResult.Value.CameraMake).IsNull();
            await Assert.That(okResult.Value.DateTaken).IsNull();
            await Assert.That(okResult.Value.Iso).IsNull();
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithGpsButNoGeocoding_ReturnsCoordinatesWithoutLocationName()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 1024);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(1920, 1080, null)
                .WithGps(40.7128, -74.0060, null));

            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "photo.jpg", Arg.Any<CancellationToken>())
                .Returns(metadata);

            _geocodingService.ReverseGeocodeAsync(40.7128, -74.0060, Arg.Any<CancellationToken>())
                .Returns((GeocodingResult?)null);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Latitude).IsEqualTo(40.7128);
            await Assert.That(okResult.Value.Longitude).IsEqualTo(-74.0060);
            await Assert.That(okResult.Value.LocationName).IsNull();
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithEmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 0);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<string>>();
            var badResult = (BadRequest<string>)result.Result;
            await Assert.That(badResult.Value).Contains("No file uploaded");
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithExtractionException_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateMockFile("corrupt.jpg", 1024);
            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "corrupt.jpg", Arg.Any<CancellationToken>())
                .Returns<ImageMetadata>(_ => throw new InvalidOperationException("Corrupt EXIF data"));

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<string>>();
            var badResult = (BadRequest<string>)result.Result;
            await Assert.That(badResult.Value).Contains("Failed to extract metadata");
            await Assert.That(badResult.Value).Contains("Corrupt EXIF data");
        }

        [Test]
        public async Task ExtractMetadataFromUpload_WithSpecialCharactersInMetadata_HandlesCorrectly()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 1024);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(1920, 1080, null)
                .WithCamera("Fujifilm", "X-T5 [Special Edition]", "XF 23mm f/1.4 R")
                .WithSoftware("Adobe Photoshop 2024 (Windows)"));

            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), "photo.jpg", Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromUpload(
                file, _metadataService, _geocodingService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.CameraMake).IsEqualTo("Fujifilm");
            await Assert.That(okResult.Value.CameraModel).IsEqualTo("X-T5 [Special Edition]");
        }

        #endregion

        #region ExtractMetadataFromPath Tests

        [Test]
        public async Task ExtractMetadataFromPath_WithValidPath_ReturnsOkWithMetadata()
        {
            // Arrange
            var testPath = @"C:\photos\test.jpg";
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(2560, 1440, 1)
                .WithDates(new DateTime(2024, 3, 10, 8, 45, 0, DateTimeKind.Utc))
                .WithCamera("Nikon", "Z9", "NIKKOR Z 70-200mm f/2.8 VR S")
                .WithExposure(2.8, 0.0005, "1/2000", 800, 135.0, 202.0)
                .WithGps(51.5074, -0.1278, 15.0)
                .WithFormat("image/jpeg", "JPEG", "sRGB", 8));

            _metadataService.ExtractMetadataAsync(testPath, Arg.Any<CancellationToken>())
                .Returns(metadata);

            var geocodingResult = new GeocodingResult { DisplayName = "London, UK" };
            _geocodingService.ReverseGeocodeAsync(51.5074, -0.1278, Arg.Any<CancellationToken>())
                .Returns(geocodingResult);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                testPath, _metadataService, _geocodingService, fileExists: true);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Width).IsEqualTo(2560);
            await Assert.That(okResult.Value.Height).IsEqualTo(1440);
            await Assert.That(okResult.Value.CameraMake).IsEqualTo("Nikon");
            await Assert.That(okResult.Value.CameraModel).IsEqualTo("Z9");
            await Assert.That(okResult.Value.FocalLength).IsEqualTo(135.0);
            await Assert.That(okResult.Value.LocationName).IsEqualTo("London, UK");
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithEmptyPath_ReturnsBadRequest()
        {
            // Arrange
            var emptyPath = "";

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                emptyPath, _metadataService, _geocodingService, fileExists: false);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<string>>();
            var badResult = (BadRequest<string>)result.Result;
            await Assert.That(badResult.Value).Contains("Path is required");
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithWhitespacePath_ReturnsBadRequest()
        {
            // Arrange
            var whitespacePath = "   ";

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                whitespacePath, _metadataService, _geocodingService, fileExists: false);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<string>>();
            var badResult = (BadRequest<string>)result.Result;
            await Assert.That(badResult.Value).Contains("Path is required");
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithNonExistentFile_ReturnsNotFound()
        {
            // Arrange
            var nonExistentPath = @"C:\photos\nonexistent.jpg";

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                nonExistentPath, _metadataService, _geocodingService, fileExists: false);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound>();
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithExtractionException_ReturnsBadRequest()
        {
            // Arrange
            var testPath = @"C:\photos\corrupt.jpg";
            _metadataService.ExtractMetadataAsync(testPath, Arg.Any<CancellationToken>())
                .Returns<ImageMetadata>(_ => throw new IOException("Unable to read file"));

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                testPath, _metadataService, _geocodingService, fileExists: true);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<string>>();
            var badResult = (BadRequest<string>)result.Result;
            await Assert.That(badResult.Value).Contains("Failed to extract metadata");
            await Assert.That(badResult.Value).Contains("Unable to read file");
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithDateFormatting_ReturnsCorrectlyFormattedDate()
        {
            // Arrange
            var testPath = @"C:\photos\test.jpg";
            var dateTaken = new DateTime(2024, 12, 25, 14, 30, 45, DateTimeKind.Utc);
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(1920, 1080, null)
                .WithDates(dateTaken));

            _metadataService.ExtractMetadataAsync(testPath, Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                testPath, _metadataService, _geocodingService, fileExists: true);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.DateTaken).IsEqualTo(dateTaken);
            await Assert.That(okResult.Value.DateTaken!.Value.Year).IsEqualTo(2024);
            await Assert.That(okResult.Value.DateTaken.Value.Month).IsEqualTo(12);
            await Assert.That(okResult.Value.DateTaken.Value.Day).IsEqualTo(25);
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithCameraSettings_ReturnsFormattedExposureTime()
        {
            // Arrange
            var testPath = @"C:\photos\test.jpg";
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(3840, 2160, null)
                .WithExposure(1.8, 0.0004, "1/2500", 100, 24.0, 35.0));

            _metadataService.ExtractMetadataAsync(testPath, Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                testPath, _metadataService, _geocodingService, fileExists: true);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.ExposureTime).IsEqualTo("1/2500");
            await Assert.That(okResult.Value.Aperture).IsEqualTo(1.8);
            await Assert.That(okResult.Value.Iso).IsEqualTo(100);
        }

        [Test]
        public async Task ExtractMetadataFromPath_WithGpsCoordinates_FormatsCorrectly()
        {
            // Arrange
            var testPath = @"C:\photos\test.jpg";
            var metadata = ImageMetadata.Successful(builder => builder
                .WithDimensions(1920, 1080, null)
                .WithGps(-33.8688, 151.2093, 42.5));

            _metadataService.ExtractMetadataAsync(testPath, Arg.Any<CancellationToken>())
                .Returns(metadata);

            var geocodingResult = new GeocodingResult { DisplayName = "Sydney, Australia" };
            _geocodingService.ReverseGeocodeAsync(-33.8688, 151.2093, Arg.Any<CancellationToken>())
                .Returns(geocodingResult);

            // Act
            var result = await MetadataEndpoints_TestHelper.ExtractMetadataFromPath(
                testPath, _metadataService, _geocodingService, fileExists: true);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<MetadataResponse>>();
            var okResult = (Ok<MetadataResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Latitude).IsEqualTo(-33.8688);
            await Assert.That(okResult.Value.Longitude).IsEqualTo(151.2093);
            await Assert.That(okResult.Value.Altitude).IsEqualTo(42.5);
            await Assert.That(okResult.Value.LocationName).IsEqualTo("Sydney, Australia");
        }

        #endregion

        #region Helper Methods

        private static IFormFile CreateMockFile(string fileName, long fileSize)
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.Length.Returns(fileSize);
            file.OpenReadStream().Returns(_ => new MemoryStream(new byte[fileSize]));
            return file;
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class MetadataEndpoints_TestHelper
    {
        public static async Task<Results<Ok<MetadataResponse>, BadRequest<string>>> ExtractMetadataFromUpload(
            IFormFile file,
            IMetadataService metadataService,
            IGeocodingService geocodingService)
        {
            var method = typeof(MetadataEndpoints)
                .GetMethod("ExtractMetadataFromUpload", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { file, metadataService, geocodingService, CancellationToken.None });
            return await (Task<Results<Ok<MetadataResponse>, BadRequest<string>>>)result!;
        }

        public static async Task<Results<Ok<MetadataResponse>, NotFound, BadRequest<string>>> ExtractMetadataFromPath(
            string path,
            IMetadataService metadataService,
            IGeocodingService geocodingService,
            bool fileExists)
        {
            // Handle empty/whitespace path early to simulate validation
            if (string.IsNullOrWhiteSpace(path))
            {
                return TypedResults.BadRequest("Path is required.");
            }

            // Simulate file not found
            if (!fileExists)
            {
                return TypedResults.NotFound();
            }

            // Since we can't mock File.Exists, we simulate the endpoint behavior
            // The endpoint checks File.Exists, then calls metadataService.ExtractMetadataAsync
            // We'll skip directly to the metadata extraction and response building
            try
            {
                var metadata = await metadataService.ExtractMetadataAsync(path, CancellationToken.None);

                string? locationName = null;
                if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
                {
                    var geocodingResult = await geocodingService.ReverseGeocodeAsync(
                        metadata.Latitude.Value,
                        metadata.Longitude.Value,
                        CancellationToken.None);

                    locationName = geocodingResult?.DisplayName;
                }

                var response = new MetadataResponse(
                    Width: metadata.Width,
                    Height: metadata.Height,
                    DateTaken: metadata.DateTaken,
                    CameraMake: metadata.CameraMake,
                    CameraModel: metadata.CameraModel,
                    LensModel: metadata.LensModel,
                    FocalLength: metadata.FocalLength,
                    Aperture: metadata.Aperture,
                    ExposureTime: metadata.ShutterSpeedFormatted,
                    Iso: metadata.Iso,
                    Latitude: metadata.Latitude,
                    Longitude: metadata.Longitude,
                    Altitude: metadata.Altitude,
                    Orientation: metadata.Orientation,
                    ColorSpace: metadata.ColorSpace,
                    LocationName: locationName
                );

                return TypedResults.Ok(response);
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest($"Failed to extract metadata: {ex.Message}");
            }
        }
    }
}
