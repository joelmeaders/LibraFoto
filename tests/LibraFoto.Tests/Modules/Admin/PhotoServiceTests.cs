using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
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
