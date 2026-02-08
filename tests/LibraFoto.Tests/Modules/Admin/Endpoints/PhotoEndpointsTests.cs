using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Endpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Shared.DTOs;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Admin.Endpoints
{
    public class PhotoEndpointsTests
    {
        private IPhotoService _photoService = null!;

        [Before(Test)]
        public void Setup()
        {
            _photoService = Substitute.For<IPhotoService>();
        }

        [Test]
        public async Task GetPhotos_ReturnsPaginatedResultFromService()
        {
            // Arrange
            var photos = new[]
            {
                new PhotoListDto(1, "photo1.jpg", "/thumb/1.jpg", 1920, 1080, MediaType.Photo,
                    DateTime.UtcNow, DateTime.UtcNow, null, 1, 2),
                new PhotoListDto(2, "photo2.jpg", "/thumb/2.jpg", 1920, 1080, MediaType.Photo,
                    DateTime.UtcNow, DateTime.UtcNow, null, 0, 1)
            };
            var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 2, 1));

            _photoService.GetPhotosAsync(Arg.Any<PhotoFilterRequest>(), Arg.Any<CancellationToken>())
                .Returns(pagedResult);

            // Act
            var result = await _photoService.GetPhotosAsync(new PhotoFilterRequest(), CancellationToken.None);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(2);
            await Assert.That(result.Pagination.TotalItems).IsEqualTo(2);
            await Assert.That(result.Pagination.Page).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotoCount_ReturnsCountFromService()
        {
            // Arrange
            var countDto = new PhotoCountDto(42);
            _photoService.GetPhotoCountAsync(Arg.Any<CancellationToken>())
                .Returns(countDto);

            // Act
            var result = await _photoService.GetPhotoCountAsync(CancellationToken.None);

            // Assert
            await Assert.That(result.Count).IsEqualTo(42);
        }

        [Test]
        public async Task GetPhotoById_ReturnsPhoto_WhenFound()
        {
            // Arrange
            var photo = new PhotoDetailDto(
                1, "photo1.jpg", "photo1.jpg", "/photos/photo1.jpg", "/thumb/1.jpg",
                1920, 1080, 5000, MediaType.Photo, null,
                DateTime.UtcNow, DateTime.UtcNow, null, null, null,
                null, null, [], []);

            _photoService.GetPhotoByIdAsync(1L, Arg.Any<CancellationToken>())
                .Returns(photo);

            // Act
            var result = await _photoService.GetPhotoByIdAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(1);
            await Assert.That(result.Filename).IsEqualTo("photo1.jpg");
        }

        [Test]
        public async Task GetPhotoById_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _photoService.GetPhotoByIdAsync(99L, Arg.Any<CancellationToken>())
                .Returns((PhotoDetailDto?)null);

            // Act
            var result = await _photoService.GetPhotoByIdAsync(99L, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdatePhoto_ReturnsUpdatedPhoto_WhenFound()
        {
            // Arrange
            var updateRequest = new UpdatePhotoRequest("renamed.jpg", "New York", DateTime.UtcNow);
            var updatedPhoto = new PhotoDetailDto(
                1, "renamed.jpg", "photo1.jpg", "/photos/photo1.jpg", "/thumb/1.jpg",
                1920, 1080, 5000, MediaType.Photo, null,
                DateTime.UtcNow, DateTime.UtcNow, "New York", null, null,
                null, null, [], []);

            _photoService.UpdatePhotoAsync(1L, Arg.Any<UpdatePhotoRequest>(), Arg.Any<CancellationToken>())
                .Returns(updatedPhoto);

            // Act
            var result = await _photoService.UpdatePhotoAsync(1L, updateRequest, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Filename).IsEqualTo("renamed.jpg");
            await Assert.That(result.Location).IsEqualTo("New York");
        }

        [Test]
        public async Task UpdatePhoto_ReturnsNull_WhenNotFound()
        {
            // Arrange
            var updateRequest = new UpdatePhotoRequest("renamed.jpg", null, null);
            _photoService.UpdatePhotoAsync(99L, Arg.Any<UpdatePhotoRequest>(), Arg.Any<CancellationToken>())
                .Returns((PhotoDetailDto?)null);

            // Act
            var result = await _photoService.UpdatePhotoAsync(99L, updateRequest, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task DeletePhoto_ReturnsTrue_WhenDeleted()
        {
            // Arrange
            _photoService.DeletePhotoAsync(1L, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _photoService.DeletePhotoAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(result).IsEqualTo(true);
        }

        [Test]
        public async Task DeletePhoto_ReturnsFalse_WhenNotFound()
        {
            // Arrange
            _photoService.DeletePhotoAsync(99L, Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await _photoService.DeletePhotoAsync(99L, CancellationToken.None);

            // Assert
            await Assert.That(result).IsEqualTo(false);
        }

        [Test]
        public async Task BulkDeletePhotos_ReturnsResultFromService()
        {
            // Arrange
            var photoIds = new long[] { 1, 2, 3 };
            var bulkResult = new BulkOperationResult(3, 0, []);
            _photoService.DeletePhotosAsync(Arg.Any<long[]>(), Arg.Any<CancellationToken>())
                .Returns(bulkResult);

            // Act
            var result = await _photoService.DeletePhotosAsync(photoIds, CancellationToken.None);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(3);
            await Assert.That(result.FailedCount).IsEqualTo(0);
            await Assert.That(result.Errors.Length).IsEqualTo(0);
        }

        [Test]
        public async Task BulkAddToAlbum_ReturnsResultFromService()
        {
            // Arrange
            var photoIds = new long[] { 1, 2 };
            var bulkResult = new BulkOperationResult(2, 0, []);
            _photoService.AddPhotosToAlbumAsync(5L, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
                .Returns(bulkResult);

            // Act
            var result = await _photoService.AddPhotosToAlbumAsync(5L, photoIds, CancellationToken.None);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsEqualTo(0);
        }

        [Test]
        public async Task BulkRemoveFromAlbum_ReturnsResultFromService()
        {
            // Arrange
            var photoIds = new long[] { 1, 2 };
            var bulkResult = new BulkOperationResult(2, 0, []);
            _photoService.RemovePhotosFromAlbumAsync(5L, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
                .Returns(bulkResult);

            // Act
            var result = await _photoService.RemovePhotosFromAlbumAsync(5L, photoIds, CancellationToken.None);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsEqualTo(0);
        }

        [Test]
        public async Task BulkAddTags_ReturnsResultFromService()
        {
            // Arrange
            var photoIds = new long[] { 1, 2, 3 };
            var tagIds = new long[] { 10, 20 };
            var bulkResult = new BulkOperationResult(3, 0, []);
            _photoService.AddTagsToPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
                .Returns(bulkResult);

            // Act
            var result = await _photoService.AddTagsToPhotosAsync(photoIds, tagIds, CancellationToken.None);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(3);
            await Assert.That(result.FailedCount).IsEqualTo(0);
        }

        [Test]
        public async Task BulkRemoveTags_ReturnsResultFromService()
        {
            // Arrange
            var photoIds = new long[] { 1, 2 };
            var tagIds = new long[] { 10 };
            var bulkResult = new BulkOperationResult(1, 1, ["Photo 2 did not have tag 10"]);
            _photoService.RemoveTagsFromPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
                .Returns(bulkResult);

            // Act
            var result = await _photoService.RemoveTagsFromPhotosAsync(photoIds, tagIds, CancellationToken.None);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(1);
            await Assert.That(result.FailedCount).IsEqualTo(1);
            await Assert.That(result.Errors.Length).IsEqualTo(1);
        }
    }
}
