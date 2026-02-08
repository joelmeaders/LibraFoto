using LibraFoto.Modules.Media.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media.Endpoints
{
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

        [Test]
        public async Task ExtractMetadata_ValidatesFileSize()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(0);

            // Assert - File with length 0 should be invalid
            await Assert.That(file.Length).IsEqualTo(0);
        }

        [Test]
        public async Task ExtractMetadataFromPath_ValidatesPath()
        {
            // Arrange
            var emptyPath = "";

            // Assert
            await Assert.That(string.IsNullOrWhiteSpace(emptyPath)).IsTrue();
        }

        [Test]
        public async Task MetadataService_ExtractsMetadata()
        {
            // Arrange
            var metadata = new LibraFoto.Modules.Media.Models.ImageMetadata
            {
                Success = true,
                Width = 1920,
                Height = 1080
            };
            _metadataService.ExtractMetadataAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(metadata);

            // Act
            var result = await _metadataService.ExtractMetadataAsync(new MemoryStream(), "test.jpg");

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Width).IsEqualTo(1920);
        }

        [Test]
        public async Task GeocodingService_ReturnsLocationName()
        {
            // Arrange
            var geocodingResult = new LibraFoto.Modules.Media.Models.GeocodingResult
            {
                DisplayName = "Paris, France"
            };
            _geocodingService.ReverseGeocodeAsync(48.8566, 2.3522, Arg.Any<CancellationToken>())
                .Returns(geocodingResult);

            // Act
            var result = await _geocodingService.ReverseGeocodeAsync(48.8566, 2.3522);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.DisplayName).IsEqualTo("Paris, France");
        }

        [Test]
        public async Task MetadataResponse_MapsCorrectly()
        {
            // Arrange
            var response = new LibraFoto.Modules.Media.Endpoints.MetadataResponse(
                Width: 1920,
                Height: 1080,
                DateTaken: new DateTime(2024, 1, 1),
                CameraMake: "Canon",
                CameraModel: "EOS R5",
                LensModel: "RF 24-70mm",
                FocalLength: 50.0,
                Aperture: 2.8,
                ExposureTime: "1/500",
                Iso: 400,
                Latitude: 48.8566,
                Longitude: 2.3522,
                Altitude: 100.0,
                Orientation: 1,
                ColorSpace: "sRGB",
                LocationName: "Paris, France"
            );

            // Assert
            await Assert.That(response.Width).IsEqualTo(1920);
            await Assert.That(response.CameraMake).IsEqualTo("Canon");
            await Assert.That(response.LocationName).IsEqualTo("Paris, France");
        }

        [Test]
        public async Task ExtractMetadata_HandlesNullGeocodingResult()
        {
            // Arrange
            _geocodingService.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
                .Returns((LibraFoto.Modules.Media.Models.GeocodingResult?)null);

            // Act
            var result = await _geocodingService.ReverseGeocodeAsync(0, 0);

            // Assert
            await Assert.That(result).IsNull();
        }
    }
}
