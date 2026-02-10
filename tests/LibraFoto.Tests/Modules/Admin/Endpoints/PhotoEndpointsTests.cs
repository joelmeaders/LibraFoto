using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Features.Photos;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Admin.Endpoints;

/// <summary>
/// Comprehensive tests for PhotoEndpoints - photo management endpoints.
/// Tests all CRUD operations, filtering, pagination, and bulk operations.
/// </summary>
public class PhotoEndpointsTests
{
    private IPhotoService _photoService = null!;

    [Before(Test)]
    public void Setup()
    {
        _photoService = Substitute.For<IPhotoService>();
    }

    #region GetPhotos Tests

    [Test]
    public async Task GetPhotos_WithDefaultParams_ReturnsPagedResult()
    {
        // Arrange
        var photos = new[]
        {
            CreatePhotoListDto(1, "photo1.jpg"),
            CreatePhotoListDto(2, "photo2.jpg")
        };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 2, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.Page == 1 && f.PageSize == 50), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest(), _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Data.Length).IsEqualTo(2);
        await Assert.That(result.Value.Pagination.Page).IsEqualTo(1);
        await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(50);
    }

    [Test]
    public async Task GetPhotos_WithCustomPagination_UsesProvidedValues()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(2, 10, 15, 2));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.Page == 2 && f.PageSize == 10), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { Page = 2, PageSize = 10 }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Pagination.Page).IsEqualTo(2);
        await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(10);
        await Assert.That(result.Value.Pagination.TotalItems).IsEqualTo(15);
    }

    [Test]
    public async Task GetPhotos_WithAlbumFilter_PassesFilterToService()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.AlbumId == 5), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { AlbumId = 5 }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).GetPhotosAsync(
            Arg.Is<PhotoFilterRequest>(f => f.AlbumId == 5),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotos_WithTagFilter_PassesFilterToService()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.TagId == 10), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { TagId = 10 }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).GetPhotosAsync(
            Arg.Is<PhotoFilterRequest>(f => f.TagId == 10),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotos_WithDateRange_PassesFilterToService()
    {
        // Arrange
        var dateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateTo = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.DateFrom == dateFrom && f.DateTo == dateTo), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { DateFrom = dateFrom, DateTo = dateTo }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).GetPhotosAsync(
            Arg.Is<PhotoFilterRequest>(f => f.DateFrom == dateFrom && f.DateTo == dateTo),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotos_WithMediaTypeFilter_PassesFilterToService()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "video.mp4", MediaType.Video) };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.MediaType == MediaType.Video), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { MediaType = MediaType.Video }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Data[0].MediaType).IsEqualTo(MediaType.Video);
    }

    [Test]
    public async Task GetPhotos_WithSearchTerm_PassesFilterToService()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "vacation.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.Search == "vacation"), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { Search = "vacation" }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).GetPhotosAsync(
            Arg.Is<PhotoFilterRequest>(f => f.Search == "vacation"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotos_WithCustomSort_PassesSortToService()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 50, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.SortBy == "Filename" && f.SortDirection == "asc"), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest { SortBy = "Filename", SortDirection = "asc" }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).GetPhotosAsync(
            Arg.Is<PhotoFilterRequest>(f => f.SortBy == "Filename" && f.SortDirection == "asc"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPhotos_WithEmptyResult_ReturnsEmptyPagedResult()
    {
        // Arrange
        var pagedResult = new PagedResult<PhotoListDto>([], new PaginationInfo(1, 50, 0, 0));

        _photoService.GetPhotosAsync(Arg.Any<PhotoFilterRequest>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest(), _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Data.Length).IsEqualTo(0);
        await Assert.That(result.Value.Pagination.TotalItems).IsEqualTo(0);
    }

    [Test]
    public async Task GetPhotos_WithMultipleFilters_CombinesFiltersCorrectly()
    {
        // Arrange
        var photos = new[] { CreatePhotoListDto(1, "photo1.jpg") };
        var pagedResult = new PagedResult<PhotoListDto>(photos, new PaginationInfo(1, 20, 1, 1));

        _photoService.GetPhotosAsync(Arg.Is<PhotoFilterRequest>(f =>
            f.AlbumId == 5 &&
            f.TagId == 10 &&
            f.MediaType == MediaType.Photo &&
            f.Search == "vacation"), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        // Act
        var result = await GetPhotosEndpoint.HandleRequestAsync(new GetPhotosRequest
        {
            AlbumId = 5,
            TagId = 10,
            MediaType = MediaType.Photo,
            Search = "vacation",
            PageSize = 20
        }, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Data.Length).IsEqualTo(1);
    }

    #endregion

    #region GetPhotoCount Tests

    [Test]
    public async Task GetPhotoCount_ReturnsCountFromService()
    {
        // Arrange
        var countDto = new PhotoCountDto(42);
        _photoService.GetPhotoCountAsync(Arg.Any<CancellationToken>())
            .Returns(countDto);

        // Act
        var result = await GetPhotoCountEndpoint.HandleRequestAsync(_photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Count).IsEqualTo(42);
    }

    [Test]
    public async Task GetPhotoCount_WithZeroPhotos_ReturnsZero()
    {
        // Arrange
        var countDto = new PhotoCountDto(0);
        _photoService.GetPhotoCountAsync(Arg.Any<CancellationToken>())
            .Returns(countDto);

        // Act
        var result = await GetPhotoCountEndpoint.HandleRequestAsync(_photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.Count).IsEqualTo(0);
    }

    #endregion

    #region GetPhotoById Tests

    [Test]
    public async Task GetPhotoById_WithValidId_ReturnsOkWithPhoto()
    {
        // Arrange
        var photo = CreatePhotoDetailDto(100, "photo100.jpg");
        _photoService.GetPhotoByIdAsync(100, Arg.Any<CancellationToken>())
            .Returns(photo);

        // Act
        var result = await GetPhotoByIdEndpoint.HandleRequestAsync(100, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
        var okResult = (Ok<PhotoDetailDto>)result.Result;
        await Assert.That(okResult.Value).IsNotNull();
        await Assert.That(okResult.Value!.Id).IsEqualTo(100);
        await Assert.That(okResult.Value.Filename).IsEqualTo("photo100.jpg");
    }

    [Test]
    public async Task GetPhotoById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _photoService.GetPhotoByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((PhotoDetailDto?)null);

        // Act
        var result = await GetPhotoByIdEndpoint.HandleRequestAsync(999, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task GetPhotoById_WithZeroId_ReturnsNotFound()
    {
        // Arrange
        _photoService.GetPhotoByIdAsync(0, Arg.Any<CancellationToken>())
            .Returns((PhotoDetailDto?)null);

        // Act
        var result = await GetPhotoByIdEndpoint.HandleRequestAsync(0, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task GetPhotoById_WithNegativeId_ReturnsNotFound()
    {
        // Arrange
        _photoService.GetPhotoByIdAsync(-1, Arg.Any<CancellationToken>())
            .Returns((PhotoDetailDto?)null);

        // Act
        var result = await GetPhotoByIdEndpoint.HandleRequestAsync(-1, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    #endregion

    #region UpdatePhoto Tests

    [Test]
    public async Task UpdatePhoto_WithValidRequest_ReturnsOkWithUpdatedPhoto()
    {
        // Arrange
        var request = new UpdatePhotoRequest("renamed.jpg", "New York", new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var updatedPhoto = CreatePhotoDetailDto(50, "renamed.jpg", "New York");
        _photoService.UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>())
            .Returns(updatedPhoto);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(50, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
        var okResult = (Ok<PhotoDetailDto>)result.Result;
        await Assert.That(okResult.Value).IsNotNull();
        await Assert.That(okResult.Value!.Filename).IsEqualTo("renamed.jpg");
        await Assert.That(okResult.Value.Location).IsEqualTo("New York");
    }

    [Test]
    public async Task UpdatePhoto_WithOnlyFilename_UpdatesFilenameOnly()
    {
        // Arrange
        var request = new UpdatePhotoRequest("newname.jpg", null, null);
        var updatedPhoto = CreatePhotoDetailDto(50, "newname.jpg");
        _photoService.UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>())
            .Returns(updatedPhoto);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(50, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
        var okResult = (Ok<PhotoDetailDto>)result.Result;
        await Assert.That(okResult.Value!.Filename).IsEqualTo("newname.jpg");
    }

    [Test]
    public async Task UpdatePhoto_WithOnlyLocation_UpdatesLocationOnly()
    {
        // Arrange
        var request = new UpdatePhotoRequest(null, "Paris, France", null);
        var updatedPhoto = CreatePhotoDetailDto(50, "photo.jpg", "Paris, France");
        _photoService.UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>())
            .Returns(updatedPhoto);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(50, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
        var okResult = (Ok<PhotoDetailDto>)result.Result;
        await Assert.That(okResult.Value!.Location).IsEqualTo("Paris, France");
    }

    [Test]
    public async Task UpdatePhoto_WithOnlyDateTaken_UpdatesDateOnly()
    {
        // Arrange
        var newDate = new DateTime(2023, 5, 20, 14, 30, 0, DateTimeKind.Utc);
        var request = new UpdatePhotoRequest(null, null, newDate);
        var updatedPhoto = CreatePhotoDetailDto(50, "photo.jpg");
        _photoService.UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>())
            .Returns(updatedPhoto);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(50, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
    }

    [Test]
    public async Task UpdatePhoto_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdatePhotoRequest("renamed.jpg", null, null);
        _photoService.UpdatePhotoAsync(999, request, Arg.Any<CancellationToken>())
            .Returns((PhotoDetailDto?)null);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(999, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task UpdatePhoto_WithAllNullValues_StillCallsService()
    {
        // Arrange
        var request = new UpdatePhotoRequest(null, null, null);
        var updatedPhoto = CreatePhotoDetailDto(50, "photo.jpg");
        _photoService.UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>())
            .Returns(updatedPhoto);

        // Act
        var result = await UpdatePhotoEndpoint.HandleRequestAsync(50, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<Ok<PhotoDetailDto>>();
        await _photoService.Received(1).UpdatePhotoAsync(50, request, Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeletePhoto Tests

    [Test]
    public async Task DeletePhoto_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _photoService.DeletePhotoAsync(50, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await DeletePhotoEndpoint.HandleRequestAsync(50, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NoContent>();
    }

    [Test]
    public async Task DeletePhoto_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _photoService.DeletePhotoAsync(999, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await DeletePhotoEndpoint.HandleRequestAsync(999, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task DeletePhoto_CallsServiceWithCorrectId()
    {
        // Arrange
        _photoService.DeletePhotoAsync(123, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await DeletePhotoEndpoint.HandleRequestAsync(123, _photoService, CancellationToken.None);

        // Assert
        await _photoService.Received(1).DeletePhotoAsync(123, Arg.Any<CancellationToken>());
    }

    #endregion

    #region BulkDeletePhotos Tests

    [Test]
    public async Task BulkDeletePhotos_WithValidIds_ReturnsSuccessResult()
    {
        // Arrange
        var request = new BulkPhotoRequest([1, 2, 3]);
        var bulkResult = new BulkOperationResult(3, 0, []);
        _photoService.DeletePhotosAsync(Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkDeletePhotosEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(3);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
        await Assert.That(result.Value.Errors.Length).IsEqualTo(0);
    }

    [Test]
    public async Task BulkDeletePhotos_WithPartialFailure_ReturnsPartialResult()
    {
        // Arrange
        var request = new BulkPhotoRequest([1, 2, 999]);
        var bulkResult = new BulkOperationResult(2, 1, ["Photo 999 not found"]);
        _photoService.DeletePhotosAsync(Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkDeletePhotosEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);
        await Assert.That(result.Value.FailedCount).IsEqualTo(1);
        await Assert.That(result.Value.Errors.Length).IsEqualTo(1);
    }

    [Test]
    public async Task BulkDeletePhotos_WithEmptyArray_ReturnsZeroResult()
    {
        // Arrange
        var request = new BulkPhotoRequest([]);
        var bulkResult = new BulkOperationResult(0, 0, []);
        _photoService.DeletePhotosAsync(Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkDeletePhotosEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(0);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
    }

    [Test]
    public async Task BulkDeletePhotos_WithAllFailures_ReturnsFailureResult()
    {
        // Arrange
        var request = new BulkPhotoRequest([998, 999]);
        var bulkResult = new BulkOperationResult(0, 2, ["Photo 998 not found", "Photo 999 not found"]);
        _photoService.DeletePhotosAsync(Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkDeletePhotosEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(0);
        await Assert.That(result.Value.FailedCount).IsEqualTo(2);
        await Assert.That(result.Value.Errors.Length).IsEqualTo(2);
    }

    #endregion

    #region BulkAddToAlbum Tests

    [Test]
    public async Task BulkAddToAlbum_WithValidIds_ReturnsSuccessResult()
    {
        // Arrange
        var request = new AddPhotosToAlbumRequest([1, 2, 3]);
        var bulkResult = new BulkOperationResult(3, 0, []);
        _photoService.AddPhotosToAlbumAsync(5, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddToAlbumEndpoint.HandleRequestAsync(5, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(3);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
    }

    [Test]
    public async Task BulkAddToAlbum_WithInvalidAlbumId_ReturnsFailureResult()
    {
        // Arrange
        var request = new AddPhotosToAlbumRequest([1, 2]);
        var bulkResult = new BulkOperationResult(0, 2, ["Album 999 not found"]);
        _photoService.AddPhotosToAlbumAsync(999, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddToAlbumEndpoint.HandleRequestAsync(999, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(0);
        await Assert.That(result.Value.FailedCount).IsEqualTo(2);
    }

    [Test]
    public async Task BulkAddToAlbum_WithPartialFailure_ReturnsPartialResult()
    {
        // Arrange
        var request = new AddPhotosToAlbumRequest([1, 999]);
        var bulkResult = new BulkOperationResult(1, 1, ["Photo 999 not found"]);
        _photoService.AddPhotosToAlbumAsync(5, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddToAlbumEndpoint.HandleRequestAsync(5, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(1);
        await Assert.That(result.Value.FailedCount).IsEqualTo(1);
    }

    #endregion

    #region BulkRemoveFromAlbum Tests

    [Test]
    public async Task BulkRemoveFromAlbum_WithValidIds_ReturnsSuccessResult()
    {
        // Arrange
        var request = new RemovePhotosFromAlbumRequest([1, 2]);
        var bulkResult = new BulkOperationResult(2, 0, []);
        _photoService.RemovePhotosFromAlbumAsync(5, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkRemoveFromAlbumEndpoint.HandleRequestAsync(5, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
    }

    [Test]
    public async Task BulkRemoveFromAlbum_WithPhotosNotInAlbum_ReturnsPartialResult()
    {
        // Arrange
        var request = new RemovePhotosFromAlbumRequest([1, 2, 3]);
        var bulkResult = new BulkOperationResult(2, 1, ["Photo 3 not in album"]);
        _photoService.RemovePhotosFromAlbumAsync(5, Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkRemoveFromAlbumEndpoint.HandleRequestAsync(5, request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);
        await Assert.That(result.Value.FailedCount).IsEqualTo(1);
    }

    #endregion

    #region BulkAddTags Tests

    [Test]
    public async Task BulkAddTags_WithValidIds_ReturnsSuccessResult()
    {
        // Arrange
        var request = new AddTagsToPhotosRequest([1, 2, 3], [10, 20]);
        var bulkResult = new BulkOperationResult(3, 0, []);
        _photoService.AddTagsToPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(3);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
    }

    [Test]
    public async Task BulkAddTags_WithInvalidPhotoIds_ReturnsPartialResult()
    {
        // Arrange
        var request = new AddTagsToPhotosRequest([1, 999], [10]);
        var bulkResult = new BulkOperationResult(1, 1, ["Photo 999 not found"]);
        _photoService.AddTagsToPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(1);
        await Assert.That(result.Value.FailedCount).IsEqualTo(1);
    }

    [Test]
    public async Task BulkAddTags_WithMultipleTags_PassesAllTags()
    {
        // Arrange
        var request = new AddTagsToPhotosRequest([1, 2], [10, 20, 30]);
        var bulkResult = new BulkOperationResult(2, 0, []);
        _photoService.AddTagsToPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkAddTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).AddTagsToPhotosAsync(
            Arg.Is<long[]>(ids => ids.Length == 2),
            Arg.Is<long[]>(ids => ids.Length == 3),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region BulkRemoveTags Tests

    [Test]
    public async Task BulkRemoveTags_WithValidIds_ReturnsSuccessResult()
    {
        // Arrange
        var request = new RemoveTagsFromPhotosRequest([1, 2], [10]);
        var bulkResult = new BulkOperationResult(2, 0, []);
        _photoService.RemoveTagsFromPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkRemoveTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);
        await Assert.That(result.Value.FailedCount).IsEqualTo(0);
    }

    [Test]
    public async Task BulkRemoveTags_WithPhotosWithoutTag_ReturnsPartialResult()
    {
        // Arrange
        var request = new RemoveTagsFromPhotosRequest([1, 2, 3], [10]);
        var bulkResult = new BulkOperationResult(2, 1, ["Photo 3 does not have tag 10"]);
        _photoService.RemoveTagsFromPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkRemoveTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);
        await Assert.That(result.Value.FailedCount).IsEqualTo(1);
        await Assert.That(result.Value.Errors.Length).IsEqualTo(1);
    }

    [Test]
    public async Task BulkRemoveTags_WithMultipleTags_PassesAllTags()
    {
        // Arrange
        var request = new RemoveTagsFromPhotosRequest([1], [10, 20, 30]);
        var bulkResult = new BulkOperationResult(1, 0, []);
        _photoService.RemoveTagsFromPhotosAsync(Arg.Any<long[]>(), Arg.Any<long[]>(), Arg.Any<CancellationToken>())
            .Returns(bulkResult);

        // Act
        var result = await BulkRemoveTagsEndpoint.HandleRequestAsync(request, _photoService, CancellationToken.None);

        // Assert
        await Assert.That(result.Value).IsNotNull();
        await _photoService.Received(1).RemoveTagsFromPhotosAsync(
            Arg.Is<long[]>(ids => ids.Length == 1),
            Arg.Is<long[]>(ids => ids.Length == 3),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private static PhotoListDto CreatePhotoListDto(long id, string filename, MediaType mediaType = MediaType.Photo)
    {
        return new PhotoListDto(
            id,
            filename,
            $"/thumb/{id}.jpg",
            1920,
            1080,
            mediaType,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            0,
            0);
    }

    private static PhotoDetailDto CreatePhotoDetailDto(long id, string filename, string? location = null)
    {
        return new PhotoDetailDto(
            id,
            filename,
            filename,
            $"/photos/{filename}",
            $"/thumb/{id}.jpg",
            1920,
            1080,
            5000,
            MediaType.Photo,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            location,
            null,
            null,
            null,
            null,
            [],
            []);
    }

    #endregion
}
