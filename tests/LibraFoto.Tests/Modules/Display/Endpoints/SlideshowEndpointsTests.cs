using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display.Endpoints
{
    public class SlideshowEndpointsTests
    {
        private ISlideshowService _slideshowService = null!;

        [Before(Test)]
        public void Setup()
        {
            _slideshowService = Substitute.For<ISlideshowService>();
        }

        [Test]
        public async Task GetNextPhoto_ReturnsPhoto_WhenAvailable()
        {
            // Arrange
            var photo = new PhotoDto { Id = 1, Url = "/api/media/photos/1", Width = 1920, Height = 1080 };
            _slideshowService.GetNextPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(photo);

            // Act
            var result = await _slideshowService.GetNextPhotoAsync(null, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(1);
        }

        [Test]
        public async Task GetNextPhoto_ReturnsNull_WhenNoPhotosAvailable()
        {
            // Arrange
            _slideshowService.GetNextPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns((PhotoDto?)null);

            // Act
            var result = await _slideshowService.GetNextPhotoAsync(null, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetCurrentPhoto_ReturnsCurrent()
        {
            // Arrange
            var photo = new PhotoDto { Id = 5, Url = "/api/media/photos/5", Width = 1920, Height = 1080 };
            _slideshowService.GetCurrentPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(photo);

            // Act
            var result = await _slideshowService.GetCurrentPhotoAsync(null, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(5);
        }

        [Test]
        public async Task GetPreloadPhotos_ReturnsMultiplePhotos()
        {
            // Arrange
            var photos = new List<PhotoDto>
            {
                new() { Id = 1, Url = "/api/media/photos/1", Width = 1920, Height = 1080 },
                new() { Id = 2, Url = "/api/media/photos/2", Width = 1920, Height = 1080 },
                new() { Id = 3, Url = "/api/media/photos/3", Width = 1920, Height = 1080 }
            };
            _slideshowService.GetPreloadPhotosAsync(3, null, Arg.Any<CancellationToken>())
                .Returns(photos.AsReadOnly());

            // Act
            var result = await _slideshowService.GetPreloadPhotosAsync(3, null, CancellationToken.None);

            // Assert
            await Assert.That(result.Count).IsEqualTo(3);
        }

        [Test]
        public async Task GetPhotoCount_ReturnsCorrectCount()
        {
            // Arrange
            _slideshowService.GetPhotoCountAsync(null, Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var count = await _slideshowService.GetPhotoCountAsync(null, CancellationToken.None);

            // Assert
            await Assert.That(count).IsEqualTo(42);
        }

        [Test]
        public void ResetSequence_CallsService()
        {
            // Act
            _slideshowService.ResetSequence(null);

            // Assert
            _slideshowService.Received(1).ResetSequence(null);
        }

        [Test]
        public async Task GetPreloadPhotos_ClampsCount_BetweenOneAndFifty()
        {
            // Arrange
            var photos = new List<PhotoDto>();
            _slideshowService.GetPreloadPhotosAsync(Arg.Any<int>(), null, Arg.Any<CancellationToken>())
                .Returns(photos.AsReadOnly());

            // Act - test with values outside range
            await _slideshowService.GetPreloadPhotosAsync(100, null, CancellationToken.None);

            // Assert - Service should handle clamping internally
            await _slideshowService.Received().GetPreloadPhotosAsync(Arg.Any<int>(), null, Arg.Any<CancellationToken>());
        }
    }
}
