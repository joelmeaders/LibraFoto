using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Admin
{
    /// <summary>
    /// Unit tests for PhotoService using SQLite in-memory database.
    /// </summary>
    public class PhotoServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private PhotoService _service = null!;
        private IConfiguration _configuration = null!;
        private IThumbnailService _thumbnailService = null!;
        private IStorageProviderFactory _providerFactory = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // Use unique database for each test to avoid concurrency issues
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection)
                // Enable detailed errors to force runtime model building instead of using compiled model
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .Options;

            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            // Setup mocks
            _configuration = Substitute.For<IConfiguration>();
            _thumbnailService = Substitute.For<IThumbnailService>();
            _providerFactory = Substitute.For<IStorageProviderFactory>();

            _service = new PhotoService(_db, _thumbnailService, _providerFactory, _configuration, NullLogger<PhotoService>.Instance);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task DeletePhotoAsync_WithNonExistentPhoto_ReturnsFalse()
        {
            // Act
            var result = await _service.DeletePhotoAsync(999);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task DeletePhotoAsync_WithExistingPhoto_DeletesFromDatabase()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "test.jpg");
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Setup mocks to succeed
            var thumbnailService = Substitute.For<IThumbnailService>();
            thumbnailService.DeleteThumbnails(Arg.Any<long>()).Returns(true);
            var service = CreateService(thumbnailService: thumbnailService);

            // Act
            var result = await service.DeletePhotoAsync(1);

            // Assert
            await Assert.That(result).IsTrue();

            var deletedPhoto = await _db.Photos.FindAsync(1L);
            await Assert.That(deletedPhoto).IsNull();
        }

        [Test]
        public async Task DeletePhotoAsync_WithStorageProvider_CallsProviderDeleteFile()
        {
            // Arrange
            await EnsureStorageProviderAsync(10);
            var photo = TestHelpers.CreateTestPhoto(id: 1, providerId: 10);
            photo.ProviderFileId = "provider-file-123";
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Setup mocks
            var provider = Substitute.For<IStorageProvider>();
            provider.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));

            var factory = Substitute.For<IStorageProviderFactory>();
            factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IStorageProvider?>(provider));

            var thumbnailService = Substitute.For<IThumbnailService>();
            thumbnailService.DeleteThumbnails(Arg.Any<long>()).Returns(true);

            var service = CreateService(thumbnailService: thumbnailService, providerFactory: factory);

            // Act
            var result = await service.DeletePhotoAsync(1);

            // Assert
            await Assert.That(result).IsTrue();
            await provider.Received(1).DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeletePhotoAsync_WhenFileDeleteFails_RollsBackDatabaseChanges()
        {
            // Arrange
            await EnsureStorageProviderAsync(10);
            var photo = TestHelpers.CreateTestPhoto(id: 1, providerId: 10);
            photo.ProviderFileId = "provider-file-123";
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Setup mocks to fail file deletion
            var provider = Substitute.For<IStorageProvider>();
            provider.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(false)); // Deletion fails

            var factory = Substitute.For<IStorageProviderFactory>();
            factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IStorageProvider?>(provider));

            var service = CreateService(providerFactory: factory);

            // Act & Assert
            await Assert.That(async () => await service.DeletePhotoAsync(1))
                .Throws<InvalidOperationException>();

            // Photo should still exist in database due to rollback
            var stillExists = await _db.Photos.FindAsync(1L);
            await Assert.That(stillExists).IsNotNull();
        }

        [Test]
        public async Task DeletePhotoAsync_CascadeDeletesPhotoAlbums()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            var photoAlbum = new PhotoAlbum { PhotoId = 1, AlbumId = 1, Photo = photo, Album = album, SortOrder = 0 };

            _db.Albums.Add(album);
            _db.Photos.Add(photo);
            _db.PhotoAlbums.Add(photoAlbum);
            await _db.SaveChangesAsync();

            // Setup mocks
            var thumbnailService = Substitute.For<IThumbnailService>();
            thumbnailService.DeleteThumbnails(Arg.Any<long>()).Returns(true);
            var service = CreateService(thumbnailService: thumbnailService);

            // Act
            var result = await service.DeletePhotoAsync(1);

            // Assert
            await Assert.That(result).IsTrue();

            // Verify cascade delete
            var remainingPhotoAlbums = await _db.PhotoAlbums.Where(pa => pa.PhotoId == 1).ToListAsync();
            await Assert.That(remainingPhotoAlbums).IsEmpty();

            // Album should still exist
            var albumExists = await _db.Albums.FindAsync(1L);
            await Assert.That(albumExists).IsNotNull();
        }

        [Test]
        public async Task DeletePhotoAsync_CascadeDeletesPhotoTags()
        {
            // Arrange
            var tag = TestHelpers.CreateTestTag(id: 1);
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            var photoTag = new PhotoTag { PhotoId = 1, TagId = 1, Photo = photo, Tag = tag };

            _db.Tags.Add(tag);
            _db.Photos.Add(photo);
            _db.PhotoTags.Add(photoTag);
            await _db.SaveChangesAsync();

            // Setup mocks
            var thumbnailService = Substitute.For<IThumbnailService>();
            thumbnailService.DeleteThumbnails(Arg.Any<long>()).Returns(true);
            var service = CreateService(thumbnailService: thumbnailService);

            // Act
            var result = await service.DeletePhotoAsync(1);

            // Assert
            await Assert.That(result).IsTrue();

            // Verify cascade delete
            var remainingPhotoTags = await _db.PhotoTags.Where(pt => pt.PhotoId == 1).ToListAsync();
            await Assert.That(remainingPhotoTags).IsEmpty();

            // Tag should still exist
            var tagExists = await _db.Tags.FindAsync(1L);
            await Assert.That(tagExists).IsNotNull();
        }

        [Test]
        public async Task DeletePhotosAsync_StopsAfterThreeFailures()
        {
            // Arrange - create 10 photos, but make deletion always fail
            await EnsureStorageProviderAsync(10);
            for (long i = 1; i <= 10; i++)
            {
                var photo = TestHelpers.CreateTestPhoto(id: i, filename: $"test{i}.jpg", providerId: 10);
                photo.ProviderFileId = $"file-{i}";
                _db.Photos.Add(photo);
            }
            await _db.SaveChangesAsync();

            // Setup mocks to always fail
            var provider = Substitute.For<IStorageProvider>();
            provider.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(false));

            var factory = Substitute.For<IStorageProviderFactory>();
            factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IStorageProvider?>(provider));

            var service = CreateService(providerFactory: factory);

            // Act
            var result = await service.DeletePhotosAsync([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(4); // 3 failures + 1 "stopped after 3 failures" message

            // All photos should still exist due to rollback
            var remainingPhotos = await _db.Photos.CountAsync();
            await Assert.That(remainingPhotos).IsEqualTo(10);
        }

        [Test]
        public async Task DeletePhotosAsync_ContinuesAfterPartialFailures()
        {
            // Arrange - create 5 photos
            await EnsureStorageProviderAsync(10);
            for (long i = 1; i <= 5; i++)
            {
                var photo = TestHelpers.CreateTestPhoto(id: i, filename: $"test{i}.jpg", providerId: i > 2 ? 10 : null);
                if (i > 2)
                {
                    photo.ProviderFileId = $"file-{i}";
                }

                _db.Photos.Add(photo);
            }
            await _db.SaveChangesAsync();

            // Setup mocks: fail for photos 3 and 4, succeed for photo 5
            var provider = Substitute.For<IStorageProvider>();
            provider.DeleteFileAsync(Arg.Is("file-3"), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(false));
            provider.DeleteFileAsync(Arg.Is("file-4"), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(false));
            provider.DeleteFileAsync(Arg.Is("file-5"), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));

            var factory = Substitute.For<IStorageProviderFactory>();
            factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IStorageProvider?>(provider));

            var thumbnailService = Substitute.For<IThumbnailService>();
            thumbnailService.DeleteThumbnails(Arg.Any<long>()).Returns(true);

            var service = CreateService(thumbnailService: thumbnailService, providerFactory: factory);

            // Act
            var result = await service.DeletePhotosAsync([1, 2, 3, 4, 5]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(3); // 1, 2, 5
            await Assert.That(result.FailedCount).IsEqualTo(2); // 3, 4
        }

        #region GetPhotosAsync Tests

        [Test]
        public async Task GetPhotosAsync_WithNoFilters_ReturnsPaginatedResults()
        {
            // Arrange
            var photos = new[]
            {
                TestHelpers.CreateTestPhoto(id: 1, filename: "photo1.jpg"),
                TestHelpers.CreateTestPhoto(id: 2, filename: "photo2.jpg"),
                TestHelpers.CreateTestPhoto(id: 3, filename: "photo3.jpg")
            };
            _db.Photos.AddRange(photos);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Page = 1, PageSize = 10 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(3);
            await Assert.That(result.Pagination.TotalItems).IsEqualTo(3);
            await Assert.That(result.Pagination.TotalPages).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_WithAlbumFilter_ReturnsPhotosInAlbum()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "in-album.jpg");
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "not-in-album.jpg");

            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { AlbumId = 1 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(1);
            await Assert.That(result.Data[0].Filename).IsEqualTo("in-album.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_WithTagFilter_ReturnsPhotosWithTag()
        {
            // Arrange
            var tag = TestHelpers.CreateTestTag(id: 1, name: "Nature");
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "tagged.jpg");
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "untagged.jpg");

            _db.Tags.Add(tag);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoTags.Add(new PhotoTag { PhotoId = 1, TagId = 1 });
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { TagId = 1 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_WithDateFromFilter_ReturnsPhotosAfterDate()
        {
            // Arrange
            var dateFrom = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "old.jpg");
            photo1.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "new.jpg");
            photo2.DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { DateFrom = dateFrom };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(2);
        }

        [Test]
        public async Task GetPhotosAsync_WithDateToFilter_ReturnsPhotosBeforeDate()
        {
            // Arrange
            var dateTo = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "old.jpg");
            photo1.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "new.jpg");
            photo2.DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { DateTo = dateTo };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_WithDateRange_ReturnsPhotosInRange()
        {
            // Arrange
            var dateFrom = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc);
            var dateTo = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "too-old.jpg");
            photo1.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "in-range.jpg");
            photo2.DateTaken = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "too-new.jpg");
            photo3.DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { DateFrom = dateFrom, DateTo = dateTo };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(2);
        }

        [Test]
        public async Task GetPhotosAsync_WithMediaTypeFilter_ReturnsCorrectMediaType()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "photo.jpg", mediaType: MediaType.Photo);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "video.mp4", mediaType: MediaType.Video);

            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { MediaType = MediaType.Video };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].MediaType).IsEqualTo(MediaType.Video);
        }

        [Test]
        public async Task GetPhotosAsync_WithSearchByFilename_ReturnsMatchingPhotos()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "sunset.jpg");
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "beach.jpg");
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "sunrise.jpg");

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Search = "sun" };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(2);
            var filenames = result.Data.Select(p => p.Filename).ToArray();
            await Assert.That(filenames).Contains("sunset.jpg");
            await Assert.That(filenames).Contains("sunrise.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_WithSearchByLocation_ReturnsMatchingPhotos()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "photo1.jpg");
            photo1.Location = "Paris, France";
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "photo2.jpg");
            photo2.Location = "London, UK";
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "photo3.jpg");
            photo3.Location = "Paris, Texas";

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Search = "paris" };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(2);
        }

        [Test]
        public async Task GetPhotosAsync_SortByDateTakenAscending_ReturnsSortedResults()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "newest.jpg");
            photo1.DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "oldest.jpg");
            photo2.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "middle.jpg");
            photo3.DateTaken = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { SortBy = "DateTaken", SortDirection = "asc" };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(3);
            await Assert.That(result.Data[0].Filename).IsEqualTo("oldest.jpg");
            await Assert.That(result.Data[1].Filename).IsEqualTo("middle.jpg");
            await Assert.That(result.Data[2].Filename).IsEqualTo("newest.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_SortByDateTakenDescending_ReturnsSortedResults()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "newest.jpg");
            photo1.DateTaken = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "oldest.jpg");
            photo2.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { SortBy = "DateTaken", SortDirection = "desc" };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data[0].Filename).IsEqualTo("newest.jpg");
            await Assert.That(result.Data[1].Filename).IsEqualTo("oldest.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_SortByFilenameAscending_ReturnsSortedResults()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "zebra.jpg");
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "apple.jpg");
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "monkey.jpg");

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { SortBy = "Filename", SortDirection = "asc" };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data[0].Filename).IsEqualTo("apple.jpg");
            await Assert.That(result.Data[1].Filename).IsEqualTo("monkey.jpg");
            await Assert.That(result.Data[2].Filename).IsEqualTo("zebra.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_DefaultSorting_SortsByDateAddedDescending()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "first.jpg");
            photo1.DateAdded = now.AddHours(-2);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "second.jpg");
            photo2.DateAdded = now.AddHours(-1);
            var photo3 = TestHelpers.CreateTestPhoto(id: 3, filename: "third.jpg");
            photo3.DateAdded = now;

            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest();

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data[0].Filename).IsEqualTo("third.jpg");
            await Assert.That(result.Data[1].Filename).IsEqualTo("second.jpg");
            await Assert.That(result.Data[2].Filename).IsEqualTo("first.jpg");
        }

        [Test]
        public async Task GetPhotosAsync_WithPaginationPageTwo_ReturnsCorrectPage()
        {
            // Arrange
            for (int i = 1; i <= 10; i++)
            {
                var photo = TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg");
                _db.Photos.Add(photo);
            }
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Page = 2, PageSize = 3 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(3);
            await Assert.That(result.Pagination.Page).IsEqualTo(2);
            await Assert.That(result.Pagination.PageSize).IsEqualTo(3);
            await Assert.That(result.Pagination.TotalPages).IsEqualTo(4); // 10 items / 3 per page = 4 pages
        }

        [Test]
        public async Task GetPhotosAsync_WithPageZero_ClampsToPageOne()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Page = 0 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Pagination.Page).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_WithNegativePage_ClampsToPageOne()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { Page = -5 };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Pagination.Page).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_WithPageSizeOverLimit_ClampsToMaximum()
        {
            // Arrange
            for (int i = 1; i <= 150; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
            }
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest { PageSize = 200 }; // Exceeds 100 limit

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(100); // Clamped to 100
            await Assert.That(result.Pagination.PageSize).IsEqualTo(100);
        }

        [Test]
        public async Task GetPhotosAsync_WithEmptyDatabase_ReturnsEmptyResult()
        {
            // Arrange
            var filter = new PhotoFilterRequest();

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data).IsEmpty();
            await Assert.That(result.Pagination.TotalItems).IsEqualTo(0);
            await Assert.That(result.Pagination.TotalPages).IsEqualTo(0);
        }

        [Test]
        public async Task GetPhotosAsync_WithCombinedFilters_ReturnsCorrectResults()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var tag = TestHelpers.CreateTestTag(id: 1);

            var photo1 = TestHelpers.CreateTestPhoto(id: 1, filename: "match.jpg", mediaType: MediaType.Photo);
            photo1.DateTaken = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            photo1.Location = "Paris";

            var photo2 = TestHelpers.CreateTestPhoto(id: 2, filename: "nomatch.jpg", mediaType: MediaType.Video);
            photo2.DateTaken = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

            _db.Albums.Add(album);
            _db.Tags.Add(tag);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
            _db.PhotoTags.Add(new PhotoTag { PhotoId = 1, TagId = 1 });
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest
            {
                AlbumId = 1,
                TagId = 1,
                MediaType = MediaType.Photo,
                DateFrom = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
                Search = "paris"
            };

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data.Length).IsEqualTo(1);
            await Assert.That(result.Data[0].Id).IsEqualTo(1);
        }

        [Test]
        public async Task GetPhotosAsync_IncludesAlbumAndTagCounts()
        {
            // Arrange
            var album1 = TestHelpers.CreateTestAlbum(id: 1);
            var album2 = TestHelpers.CreateTestAlbum(id: 2);
            var tag1 = TestHelpers.CreateTestTag(id: 1);
            var tag2 = TestHelpers.CreateTestTag(id: 2);
            var photo = TestHelpers.CreateTestPhoto(id: 1);

            _db.Albums.AddRange(album1, album2);
            _db.Tags.AddRange(tag1, tag2);
            _db.Photos.Add(photo);
            _db.PhotoAlbums.AddRange(
                new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 },
                new PhotoAlbum { PhotoId = 1, AlbumId = 2, SortOrder = 0 }
            );
            _db.PhotoTags.AddRange(
                new PhotoTag { PhotoId = 1, TagId = 1 },
                new PhotoTag { PhotoId = 1, TagId = 2 }
            );
            await _db.SaveChangesAsync();

            var filter = new PhotoFilterRequest();

            // Act
            var result = await _service.GetPhotosAsync(filter);

            // Assert
            await Assert.That(result.Data[0].AlbumCount).IsEqualTo(2);
            await Assert.That(result.Data[0].TagCount).IsEqualTo(2);
        }

        #endregion

        #region GetPhotoByIdAsync Tests

        [Test]
        public async Task GetPhotoByIdAsync_WithExistingPhoto_ReturnsFullDetails()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "test.jpg");
            photo.OriginalFilename = "original.jpg";
            photo.Location = "Paris";
            photo.Latitude = 48.8566;
            photo.Longitude = 2.3522;
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetPhotoByIdAsync(1);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(1);
            await Assert.That(result.Filename).IsEqualTo("test.jpg");
            await Assert.That(result.OriginalFilename).IsEqualTo("original.jpg");
            await Assert.That(result.Location).IsEqualTo("Paris");
            await Assert.That(result.Latitude).IsEqualTo(48.8566);
            await Assert.That(result.Longitude).IsEqualTo(2.3522);
        }

        [Test]
        public async Task GetPhotoByIdAsync_WithNonExistentPhoto_ReturnsNull()
        {
            // Act
            var result = await _service.GetPhotoByIdAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetPhotoByIdAsync_IncludesAlbums()
        {
            // Arrange
            var album1 = TestHelpers.CreateTestAlbum(id: 1, name: "Album 1");
            var album2 = TestHelpers.CreateTestAlbum(id: 2, name: "Album 2");
            var photo = TestHelpers.CreateTestPhoto(id: 1);

            _db.Albums.AddRange(album1, album2);
            _db.Photos.Add(photo);
            _db.PhotoAlbums.AddRange(
                new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 },
                new PhotoAlbum { PhotoId = 1, AlbumId = 2, SortOrder = 1 }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetPhotoByIdAsync(1);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Albums.Length).IsEqualTo(2);
            await Assert.That(result.Albums.Any(a => a.Name == "Album 1")).IsTrue();
            await Assert.That(result.Albums.Any(a => a.Name == "Album 2")).IsTrue();
        }

        [Test]
        public async Task GetPhotoByIdAsync_IncludesTags()
        {
            // Arrange
            var tag1 = TestHelpers.CreateTestTag(id: 1, name: "Nature", color: "#00FF00");
            var tag2 = TestHelpers.CreateTestTag(id: 2, name: "Landscape", color: "#0000FF");
            var photo = TestHelpers.CreateTestPhoto(id: 1);

            _db.Tags.AddRange(tag1, tag2);
            _db.Photos.Add(photo);
            _db.PhotoTags.AddRange(
                new PhotoTag { PhotoId = 1, TagId = 1 },
                new PhotoTag { PhotoId = 1, TagId = 2 }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetPhotoByIdAsync(1);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Tags.Length).IsEqualTo(2);
            await Assert.That(result.Tags.Any(t => t.Name == "Nature" && t.Color == "#00FF00")).IsTrue();
            await Assert.That(result.Tags.Any(t => t.Name == "Landscape" && t.Color == "#0000FF")).IsTrue();
        }

        [Test]
        public async Task GetPhotoByIdAsync_WithStorageProvider_IncludesProviderName()
        {
            // Arrange
            await EnsureStorageProviderAsync(10);
            var photo = TestHelpers.CreateTestPhoto(id: 1, providerId: 10);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetPhotoByIdAsync(1);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ProviderId).IsEqualTo(10);
            await Assert.That(result.ProviderName).IsEqualTo("Test Provider");
        }

        #endregion

        #region UpdatePhotoAsync Tests

        [Test]
        public async Task UpdatePhotoAsync_WithNonExistentPhoto_ReturnsNull()
        {
            // Arrange
            var request = new UpdatePhotoRequest("new.jpg", null, null);

            // Act
            var result = await _service.UpdatePhotoAsync(999, request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdatePhotoAsync_UpdatesFilename()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "old.jpg");
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var request = new UpdatePhotoRequest("new-filename.jpg", null, null);

            // Act
            var result = await _service.UpdatePhotoAsync(1, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Filename).IsEqualTo("new-filename.jpg");

            // Verify database
            var updated = await _db.Photos.FindAsync(1L);
            await Assert.That(updated!.Filename).IsEqualTo("new-filename.jpg");
        }

        [Test]
        public async Task UpdatePhotoAsync_UpdatesLocation()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            photo.Location = "Old Location";
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var request = new UpdatePhotoRequest(null, "New Location", null);

            // Act
            var result = await _service.UpdatePhotoAsync(1, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Location).IsEqualTo("New Location");
        }

        [Test]
        public async Task UpdatePhotoAsync_UpdatesDateTaken()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            photo.DateTaken = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var newDate = new DateTime(2026, 5, 15, 14, 30, 0, DateTimeKind.Utc);
            var request = new UpdatePhotoRequest(null, null, newDate);

            // Act
            var result = await _service.UpdatePhotoAsync(1, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.DateTaken).IsEqualTo(newDate);
        }

        [Test]
        public async Task UpdatePhotoAsync_UpdatesAllFields()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var newDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
            var request = new UpdatePhotoRequest("updated.jpg", "Updated Location", newDate);

            // Act
            var result = await _service.UpdatePhotoAsync(1, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Filename).IsEqualTo("updated.jpg");
            await Assert.That(result.Location).IsEqualTo("Updated Location");
            await Assert.That(result.DateTaken).IsEqualTo(newDate);
        }

        [Test]
        public async Task UpdatePhotoAsync_WithNullFields_DoesNotUpdateThem()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "original.jpg");
            photo.Location = "Original Location";
            photo.DateTaken = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            var request = new UpdatePhotoRequest(null, null, null);

            // Act
            var result = await _service.UpdatePhotoAsync(1, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Filename).IsEqualTo("original.jpg");
            await Assert.That(result.Location).IsEqualTo("Original Location");
            await Assert.That(result.DateTaken).IsEqualTo(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        #endregion

        #region GetPhotoCountAsync Tests

        [Test]
        public async Task GetPhotoCountAsync_WithNoPhotos_ReturnsZero()
        {
            // Act
            var result = await _service.GetPhotoCountAsync();

            // Assert
            await Assert.That(result.Count).IsEqualTo(0);
        }

        [Test]
        public async Task GetPhotoCountAsync_WithMultiplePhotos_ReturnsCorrectCount()
        {
            // Arrange
            for (int i = 1; i <= 42; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
            }
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetPhotoCountAsync();

            // Assert
            await Assert.That(result.Count).IsEqualTo(42);
        }

        #endregion

        #region AddPhotosToAlbumAsync Tests

        [Test]
        public async Task AddPhotosToAlbumAsync_WithNonExistentAlbum_ReturnsError()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosToAlbumAsync(999, [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(1);
            await Assert.That(result.Errors.Length).IsEqualTo(1);
            await Assert.That(result.Errors[0]).Contains("Album 999 not found");
        }

        [Test]
        public async Task AddPhotosToAlbumAsync_WithNonExistentPhotos_ReturnsErrors()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosToAlbumAsync(1, [999, 1000]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 999 not found"))).IsTrue();
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 1000 not found"))).IsTrue();
        }

        [Test]
        public async Task AddPhotosToAlbumAsync_WithValidPhotos_AddsToAlbum()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosToAlbumAsync(1, [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var photoAlbums = await _db.PhotoAlbums.Where(pa => pa.AlbumId == 1).ToListAsync();
            await Assert.That(photoAlbums.Count).IsEqualTo(2);
        }

        [Test]
        public async Task AddPhotosToAlbumAsync_CalculatesSortOrderCorrectly()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            var photo3 = TestHelpers.CreateTestPhoto(id: 3);

            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2, photo3);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 5 });
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosToAlbumAsync(1, [2, 3]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);

            var photoAlbums = await _db.PhotoAlbums
                .Where(pa => pa.AlbumId == 1 && (pa.PhotoId == 2 || pa.PhotoId == 3))
                .OrderBy(pa => pa.SortOrder)
                .ToListAsync();

            await Assert.That(photoAlbums[0].SortOrder).IsEqualTo(6);
            await Assert.That(photoAlbums[1].SortOrder).IsEqualTo(7);
        }

        [Test]
        public async Task AddPhotosToAlbumAsync_WithPhotoAlreadyInAlbum_ReturnsError()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Albums.Add(album);
            _db.Photos.Add(photo);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosToAlbumAsync(1, [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(1);
            await Assert.That(result.Errors[0]).Contains("Photo 1 already in album");
        }

        [Test]
        public async Task AddPhotosToAlbumAsync_WithMixedValidAndInvalid_ProcessesCorrectly()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
            await _db.SaveChangesAsync();

            // Act - try to add photos 1 (already in), 2 (valid), 999 (doesn't exist)
            var result = await _service.AddPhotosToAlbumAsync(1, [1, 2, 999]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(1); // Only photo 2 added
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 1 already in album"))).IsTrue();
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 999 not found"))).IsTrue();
        }

        #endregion

        #region RemovePhotosFromAlbumAsync Tests

        [Test]
        public async Task RemovePhotosFromAlbumAsync_WithNonExistentAlbum_ReturnsError()
        {
            // Act
            var result = await _service.RemovePhotosFromAlbumAsync(999, [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors[0]).Contains("Album 999 not found");
        }

        [Test]
        public async Task RemovePhotosFromAlbumAsync_WithPhotosInAlbum_RemovesThem()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoAlbums.AddRange(
                new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 },
                new PhotoAlbum { PhotoId = 2, AlbumId = 1, SortOrder = 1 }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.RemovePhotosFromAlbumAsync(1, [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var remaining = await _db.PhotoAlbums.Where(pa => pa.AlbumId == 1).ToListAsync();
            await Assert.That(remaining).IsEmpty();
        }

        [Test]
        public async Task RemovePhotosFromAlbumAsync_WithPhotoNotInAlbum_ReturnsError()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Albums.Add(album);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.RemovePhotosFromAlbumAsync(1, [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(1);
            await Assert.That(result.Errors[0]).Contains("Photo 1 not in album");
        }

        [Test]
        public async Task RemovePhotosFromAlbumAsync_WithMixedPhotos_ProcessesCorrectly()
        {
            // Arrange
            var album = TestHelpers.CreateTestAlbum(id: 1);
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            _db.Albums.Add(album);
            _db.Photos.AddRange(photo1, photo2);
            _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
            await _db.SaveChangesAsync();

            // Act - try to remove photo 1 (in album) and photo 2 (not in album)
            var result = await _service.RemovePhotosFromAlbumAsync(1, [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(1);
            await Assert.That(result.FailedCount).IsEqualTo(1);
            await Assert.That(result.Errors[0]).Contains("Photo 2 not in album");
        }

        #endregion

        #region AddTagsToPhotosAsync Tests

        [Test]
        public async Task AddTagsToPhotosAsync_WithNonExistentPhotos_ReturnsErrors()
        {
            // Arrange
            var tag = TestHelpers.CreateTestTag(id: 1);
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddTagsToPhotosAsync([999, 1000], [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 999 not found"))).IsTrue();
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 1000 not found"))).IsTrue();
        }

        [Test]
        public async Task AddTagsToPhotosAsync_WithNonExistentTags_ReturnsErrors()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddTagsToPhotosAsync([1], [999, 1000]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Any(e => e.Contains("Tag 999 not found"))).IsTrue();
            await Assert.That(result.Errors.Any(e => e.Contains("Tag 1000 not found"))).IsTrue();
        }

        [Test]
        public async Task AddTagsToPhotosAsync_WithValidData_AddsAllCombinations()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            var tag1 = TestHelpers.CreateTestTag(id: 1);
            var tag2 = TestHelpers.CreateTestTag(id: 2);
            _db.Photos.AddRange(photo1, photo2);
            _db.Tags.AddRange(tag1, tag2);
            await _db.SaveChangesAsync();

            // Act - 2 photos × 2 tags = 4 combinations
            var result = await _service.AddTagsToPhotosAsync([1, 2], [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(4);
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var photoTags = await _db.PhotoTags.ToListAsync();
            await Assert.That(photoTags.Count).IsEqualTo(4);
            await Assert.That(photoTags.Any(pt => pt.PhotoId == 1 && pt.TagId == 1)).IsTrue();
            await Assert.That(photoTags.Any(pt => pt.PhotoId == 1 && pt.TagId == 2)).IsTrue();
            await Assert.That(photoTags.Any(pt => pt.PhotoId == 2 && pt.TagId == 1)).IsTrue();
            await Assert.That(photoTags.Any(pt => pt.PhotoId == 2 && pt.TagId == 2)).IsTrue();
        }

        [Test]
        public async Task AddTagsToPhotosAsync_SkipsExistingCombinations()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            var tag1 = TestHelpers.CreateTestTag(id: 1);
            var tag2 = TestHelpers.CreateTestTag(id: 2);
            _db.Photos.Add(photo);
            _db.Tags.AddRange(tag1, tag2);
            _db.PhotoTags.Add(new PhotoTag { PhotoId = 1, TagId = 1 }); // Already exists
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddTagsToPhotosAsync([1], [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(1); // Only tag2 added
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var photoTags = await _db.PhotoTags.ToListAsync();
            await Assert.That(photoTags.Count).IsEqualTo(2); // Original + new one
        }

        [Test]
        public async Task AddTagsToPhotosAsync_WithMixedValidAndInvalid_ProcessesCorrectly()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            var tag = TestHelpers.CreateTestTag(id: 1);
            _db.Photos.Add(photo);
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act - valid photo 1, invalid photo 999, valid tag 1, invalid tag 888
            var result = await _service.AddTagsToPhotosAsync([1, 999], [1, 888]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(1); // Only 1×1
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Any(e => e.Contains("Photo 999 not found"))).IsTrue();
            await Assert.That(result.Errors.Any(e => e.Contains("Tag 888 not found"))).IsTrue();
        }

        #endregion

        #region RemoveTagsFromPhotosAsync Tests

        [Test]
        public async Task RemoveTagsFromPhotosAsync_RemovesMatchingCombinations()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            var tag1 = TestHelpers.CreateTestTag(id: 1);
            var tag2 = TestHelpers.CreateTestTag(id: 2);
            _db.Photos.AddRange(photo1, photo2);
            _db.Tags.AddRange(tag1, tag2);
            _db.PhotoTags.AddRange(
                new PhotoTag { PhotoId = 1, TagId = 1 },
                new PhotoTag { PhotoId = 1, TagId = 2 },
                new PhotoTag { PhotoId = 2, TagId = 1 },
                new PhotoTag { PhotoId = 2, TagId = 2 }
            );
            await _db.SaveChangesAsync();

            // Act - remove tag 1 from both photos
            var result = await _service.RemoveTagsFromPhotosAsync([1, 2], [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var remaining = await _db.PhotoTags.ToListAsync();
            await Assert.That(remaining.Count).IsEqualTo(2); // Only tag2 associations remain
            await Assert.That(remaining.All(pt => pt.TagId == 2)).IsTrue();
        }

        [Test]
        public async Task RemoveTagsFromPhotosAsync_WithNonExistentCombinations_ReturnsSuccess()
        {
            // Arrange
            var photo = TestHelpers.CreateTestPhoto(id: 1);
            var tag = TestHelpers.CreateTestTag(id: 1);
            _db.Photos.Add(photo);
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act - try to remove non-existent association
            var result = await _service.RemoveTagsFromPhotosAsync([1], [1]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(0);
            await Assert.That(result.FailedCount).IsEqualTo(0);
        }

        [Test]
        public async Task RemoveTagsFromPhotosAsync_RemovesAllMatchingCombinations()
        {
            // Arrange
            var photo1 = TestHelpers.CreateTestPhoto(id: 1);
            var photo2 = TestHelpers.CreateTestPhoto(id: 2);
            var tag1 = TestHelpers.CreateTestTag(id: 1);
            var tag2 = TestHelpers.CreateTestTag(id: 2);
            _db.Photos.AddRange(photo1, photo2);
            _db.Tags.AddRange(tag1, tag2);
            _db.PhotoTags.AddRange(
                new PhotoTag { PhotoId = 1, TagId = 1 },
                new PhotoTag { PhotoId = 1, TagId = 2 },
                new PhotoTag { PhotoId = 2, TagId = 1 },
                new PhotoTag { PhotoId = 2, TagId = 2 }
            );
            await _db.SaveChangesAsync();

            // Act - remove all combinations
            var result = await _service.RemoveTagsFromPhotosAsync([1, 2], [1, 2]);

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(4);
            await Assert.That(result.FailedCount).IsEqualTo(0);

            var remaining = await _db.PhotoTags.ToListAsync();
            await Assert.That(remaining).IsEmpty();
        }

        #endregion

        private async Task EnsureStorageProviderAsync(long providerId)
        {
            var existing = await _db.StorageProviders.FindAsync(providerId);
            if (existing != null)
            {
                return;
            }

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(
                providerId,
                name: "Test Provider",
                type: StorageProviderType.GooglePhotos));
            await _db.SaveChangesAsync();
        }

        private PhotoService CreateService(
            IThumbnailService? thumbnailService = null,
            IStorageProviderFactory? providerFactory = null,
            IConfiguration? configuration = null)
        {
            return new PhotoService(
                _db,
                thumbnailService ?? _thumbnailService,
                providerFactory ?? _providerFactory,
                configuration ?? _configuration,
                NullLogger<PhotoService>.Instance);
        }
    }
}
