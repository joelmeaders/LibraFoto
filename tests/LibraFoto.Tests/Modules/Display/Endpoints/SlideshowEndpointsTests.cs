using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Endpoints;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display.Endpoints
{
    /// <summary>
    /// Comprehensive tests for SlideshowEndpoints.
    /// Tests all endpoints covering success and error paths, query parameter variations, and edge cases.
    /// </summary>
    public class SlideshowEndpointsTests
    {
        private ISlideshowService _slideshowService = null!;

        [Before(Test)]
        public void Setup()
        {
            _slideshowService = Substitute.For<ISlideshowService>();
        }

        #region GetNextPhoto Tests

        [Test]
        public async Task GetNextPhoto_WithoutSettingsId_ReturnsPhoto_WhenAvailable()
        {
            // Arrange
            var expectedPhoto = CreateSamplePhoto(1);
            _slideshowService.GetNextPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(expectedPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetNextPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(1);
            await Assert.That(okResult.Value.Url).IsEqualTo("/api/media/photos/1");
        }

        [Test]
        public async Task GetNextPhoto_WithSettingsId_ReturnsPhoto()
        {
            // Arrange
            var expectedPhoto = CreateSamplePhoto(10);
            _slideshowService.GetNextPhotoAsync(5L, Arg.Any<CancellationToken>())
                .Returns(expectedPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetNextPhoto(5L, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(10);
        }

        [Test]
        public async Task GetNextPhoto_WhenNoPhotosAvailable_ReturnsNotFound()
        {
            // Arrange
            _slideshowService.GetNextPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns((PhotoDto?)null);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetNextPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value).IsNotNull();
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("NO_PHOTOS_AVAILABLE");
            await Assert.That(notFoundResult.Value.Message).Contains("No photos are available");
        }

        [Test]
        public async Task GetNextPhoto_WhenNoPhotosAvailable_WithSettingsId_ReturnsNotFound()
        {
            // Arrange
            _slideshowService.GetNextPhotoAsync(3L, Arg.Any<CancellationToken>())
                .Returns((PhotoDto?)null);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetNextPhoto(3L, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("NO_PHOTOS_AVAILABLE");
        }

        [Test]
        public async Task GetNextPhoto_ReturnsPhotoWithAllProperties()
        {
            // Arrange
            var expectedPhoto = new PhotoDto
            {
                Id = 42,
                Url = "/api/media/photos/42",
                ThumbnailUrl = "/api/media/thumbnails/42",
                DateTaken = new DateTime(2024, 1, 15),
                Location = "New York, USA",
                MediaType = MediaType.Photo,
                Duration = null,
                Width = 3840,
                Height = 2160
            };
            _slideshowService.GetNextPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(expectedPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetNextPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            var photo = okResult.Value!;
            await Assert.That(photo.Id).IsEqualTo(42);
            await Assert.That(photo.ThumbnailUrl).IsEqualTo("/api/media/thumbnails/42");
            await Assert.That(photo.Location).IsEqualTo("New York, USA");
            await Assert.That(photo.Width).IsEqualTo(3840);
            await Assert.That(photo.Height).IsEqualTo(2160);
        }

        #endregion

        #region GetCurrentPhoto Tests

        [Test]
        public async Task GetCurrentPhoto_WithoutSettingsId_ReturnsPhoto_WhenAvailable()
        {
            // Arrange
            var expectedPhoto = CreateSamplePhoto(5);
            _slideshowService.GetCurrentPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(expectedPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetCurrentPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(5);
        }

        [Test]
        public async Task GetCurrentPhoto_WithSettingsId_ReturnsPhoto()
        {
            // Arrange
            var expectedPhoto = CreateSamplePhoto(7);
            _slideshowService.GetCurrentPhotoAsync(2L, Arg.Any<CancellationToken>())
                .Returns(expectedPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetCurrentPhoto(2L, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            await Assert.That(okResult.Value!.Id).IsEqualTo(7);
        }

        [Test]
        public async Task GetCurrentPhoto_WhenNoPhotosAvailable_ReturnsNotFound()
        {
            // Arrange
            _slideshowService.GetCurrentPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns((PhotoDto?)null);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetCurrentPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFoundResult = (NotFound<ApiError>)result.Result;
            await Assert.That(notFoundResult.Value).IsNotNull();
            await Assert.That(notFoundResult.Value!.Code).IsEqualTo("NO_PHOTOS_AVAILABLE");
            await Assert.That(notFoundResult.Value.Message).Contains("No photos are available");
        }

        [Test]
        public async Task GetCurrentPhoto_WhenNoPhotosAvailable_WithSettingsId_ReturnsNotFound()
        {
            // Arrange
            _slideshowService.GetCurrentPhotoAsync(99L, Arg.Any<CancellationToken>())
                .Returns((PhotoDto?)null);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetCurrentPhoto(99L, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task GetCurrentPhoto_ReturnsVideoWithDuration()
        {
            // Arrange
            var videoPhoto = new PhotoDto
            {
                Id = 100,
                Url = "/api/media/photos/100",
                ThumbnailUrl = "/api/media/thumbnails/100",
                MediaType = MediaType.Video,
                Duration = 125.5,
                Width = 1920,
                Height = 1080
            };
            _slideshowService.GetCurrentPhotoAsync(null, Arg.Any<CancellationToken>())
                .Returns(videoPhoto);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetCurrentPhoto(null, _slideshowService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PhotoDto>>();
            var okResult = (Ok<PhotoDto>)result.Result;
            var photo = okResult.Value!;
            await Assert.That(photo.MediaType).IsEqualTo(MediaType.Video);
            await Assert.That(photo.Duration).IsEqualTo(125.5);
        }

        #endregion

        #region GetPreloadPhotos Tests

        [Test]
        public async Task GetPreloadPhotos_WithDefaultCount_ReturnsPhotos()
        {
            // Arrange
            var photos = CreatePhotoList(10);
            _slideshowService.GetPreloadPhotosAsync(10, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(null, null, _slideshowService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Count).IsEqualTo(10);
        }

        [Test]
        public async Task GetPreloadPhotos_WithCustomCount_ReturnsRequestedPhotos()
        {
            // Arrange
            var photos = CreatePhotoList(5);
            _slideshowService.GetPreloadPhotosAsync(5, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(5, null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(5);
        }

        [Test]
        public async Task GetPreloadPhotos_WithSettingsId_PassesSettingsToService()
        {
            // Arrange
            var photos = CreatePhotoList(3);
            _slideshowService.GetPreloadPhotosAsync(3, 7L, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(3, 7L, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(3);
            await _slideshowService.Received(1).GetPreloadPhotosAsync(3, 7L, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithCountBelowMinimum_ClampsToOne()
        {
            // Arrange
            var photos = CreatePhotoList(1);
            _slideshowService.GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act - request 0, should be clamped to 1
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(0, null, _slideshowService);

            // Assert
            await _slideshowService.Received(1).GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithNegativeCount_ClampsToOne()
        {
            // Arrange
            var photos = CreatePhotoList(1);
            _slideshowService.GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act - request -5, should be clamped to 1
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(-5, null, _slideshowService);

            // Assert
            await _slideshowService.Received(1).GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithCountAboveMaximum_ClampsToFifty()
        {
            // Arrange
            var photos = CreatePhotoList(50);
            _slideshowService.GetPreloadPhotosAsync(50, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act - request 100, should be clamped to 50
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(100, null, _slideshowService);

            // Assert
            await _slideshowService.Received(1).GetPreloadPhotosAsync(50, null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithExactlyFifty_DoesNotClamp()
        {
            // Arrange
            var photos = CreatePhotoList(50);
            _slideshowService.GetPreloadPhotosAsync(50, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(50, null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(50);
            await _slideshowService.Received(1).GetPreloadPhotosAsync(50, null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithExactlyOne_DoesNotClamp()
        {
            // Arrange
            var photos = CreatePhotoList(1);
            _slideshowService.GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(1, null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(1);
            await _slideshowService.Received(1).GetPreloadPhotosAsync(1, null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPreloadPhotos_WithEmptyResult_ReturnsEmptyList()
        {
            // Arrange
            var emptyList = new List<PhotoDto>().AsReadOnly();
            _slideshowService.GetPreloadPhotosAsync(10, null, Arg.Any<CancellationToken>())
                .Returns(emptyList);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(10, null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task GetPreloadPhotos_CountTwentyFive_MidRange_DoesNotClamp()
        {
            // Arrange
            var photos = CreatePhotoList(25);
            _slideshowService.GetPreloadPhotosAsync(25, null, Arg.Any<CancellationToken>())
                .Returns(photos);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPreloadPhotos(25, null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(25);
            await _slideshowService.Received(1).GetPreloadPhotosAsync(25, null, Arg.Any<CancellationToken>());
        }

        #endregion

        #region GetPhotoCount Tests

        [Test]
        public async Task GetPhotoCount_WithoutSettingsId_ReturnsCount()
        {
            // Arrange
            _slideshowService.GetPhotoCountAsync(null, Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPhotoCount(null, _slideshowService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.TotalPhotos).IsEqualTo(42);
        }

        [Test]
        public async Task GetPhotoCount_WithSettingsId_ReturnsCount()
        {
            // Arrange
            _slideshowService.GetPhotoCountAsync(5L, Arg.Any<CancellationToken>())
                .Returns(100);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPhotoCount(5L, _slideshowService);

            // Assert
            await Assert.That(result.Value!.TotalPhotos).IsEqualTo(100);
        }

        [Test]
        public async Task GetPhotoCount_WhenZeroPhotos_ReturnsZero()
        {
            // Arrange
            _slideshowService.GetPhotoCountAsync(null, Arg.Any<CancellationToken>())
                .Returns(0);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPhotoCount(null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.TotalPhotos).IsEqualTo(0);
        }

        [Test]
        public async Task GetPhotoCount_LargeNumber_ReturnsCorrectCount()
        {
            // Arrange
            _slideshowService.GetPhotoCountAsync(null, Arg.Any<CancellationToken>())
                .Returns(999999);

            // Act
            var result = await SlideshowEndpoints_TestHelper.GetPhotoCount(null, _slideshowService);

            // Assert
            await Assert.That(result.Value!.TotalPhotos).IsEqualTo(999999);
        }

        #endregion

        #region ResetSequence Tests

        [Test]
        public async Task ResetSequence_WithoutSettingsId_CallsServiceAndReturnsSuccess()
        {
            // Act
            var result = SlideshowEndpoints_TestHelper.ResetSequence(null, _slideshowService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Success).IsTrue();
            await Assert.That(result.Value.Message).Contains("reset");
            _slideshowService.Received(1).ResetSequence(null);
        }

        [Test]
        public async Task ResetSequence_WithSettingsId_CallsServiceAndReturnsSuccess()
        {
            // Act
            var result = SlideshowEndpoints_TestHelper.ResetSequence(10L, _slideshowService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Success).IsTrue();
            await Assert.That(result.Value.Message).Contains("Slideshow sequence has been reset");
            _slideshowService.Received(1).ResetSequence(10L);
        }

        #endregion

        #region Helper Methods

        private static PhotoDto CreateSamplePhoto(long id)
        {
            return new PhotoDto
            {
                Id = id,
                Url = $"/api/media/photos/{id}",
                ThumbnailUrl = $"/api/media/thumbnails/{id}",
                Width = 1920,
                Height = 1080,
                MediaType = MediaType.Photo
            };
        }

        private static IReadOnlyList<PhotoDto> CreatePhotoList(int count)
        {
            var photos = new List<PhotoDto>();
            for (int i = 1; i <= count; i++)
            {
                photos.Add(CreateSamplePhoto(i));
            }
            return photos.AsReadOnly();
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class SlideshowEndpoints_TestHelper
    {
        public static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> GetNextPhoto(
            long? settingsId, ISlideshowService service)
        {
            var method = typeof(SlideshowEndpoints)
                .GetMethod("GetNextPhoto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { settingsId, service, CancellationToken.None });
            return await (Task<Results<Ok<PhotoDto>, NotFound<ApiError>>>)result!;
        }

        public static async Task<Results<Ok<PhotoDto>, NotFound<ApiError>>> GetCurrentPhoto(
            long? settingsId, ISlideshowService service)
        {
            var method = typeof(SlideshowEndpoints)
                .GetMethod("GetCurrentPhoto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { settingsId, service, CancellationToken.None });
            return await (Task<Results<Ok<PhotoDto>, NotFound<ApiError>>>)result!;
        }

        public static async Task<Ok<IReadOnlyList<PhotoDto>>> GetPreloadPhotos(
            int? count, long? settingsId, ISlideshowService service)
        {
            var method = typeof(SlideshowEndpoints)
                .GetMethod("GetPreloadPhotos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { count, settingsId, service, CancellationToken.None });
            return await (Task<Ok<IReadOnlyList<PhotoDto>>>)result!;
        }

        public static async Task<Ok<PhotoCountResponse>> GetPhotoCount(
            long? settingsId, ISlideshowService service)
        {
            var method = typeof(SlideshowEndpoints)
                .GetMethod("GetPhotoCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { settingsId, service, CancellationToken.None });
            return await (Task<Ok<PhotoCountResponse>>)result!;
        }

        public static Ok<ResetResponse> ResetSequence(
            long? settingsId, ISlideshowService service)
        {
            var method = typeof(SlideshowEndpoints)
                .GetMethod("ResetSequence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { settingsId, service });
            return (Ok<ResetResponse>)result!;
        }
    }
}
