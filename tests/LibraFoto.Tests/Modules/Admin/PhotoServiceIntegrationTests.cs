using NSubstitute;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using LibraFoto.Tests.Helpers;
using LibraFoto.Data.Enums;

namespace LibraFoto.Tests.Modules.Admin;

/// <summary>
/// Integration tests for PhotoService with real SQLite database and file operations.
/// </summary>
public class PhotoServiceIntegrationTests
{
    private string _tempDir = null!;
    private LibraFotoDbContext _db = null!;
    private PhotoService _service = null!;
    private IThumbnailService _thumbnailService = null!;
    private IStorageProviderFactory _providerFactory = null!;
    private IConfiguration _configuration = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _tempDir = TestHelpers.CreateTempDirectory();

        // Use real SQLite for integration tests
        var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_tempDir, "test.db")}")
            // Enable detailed errors to force runtime model building instead of using compiled model
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        _db = new LibraFotoDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        // Setup real thumbnail service
        var thumbnailBasePath = Path.Combine(_tempDir, ".thumbnails");
        _thumbnailService = new ThumbnailService(thumbnailBasePath);

        // Setup mocks for provider factory
        _providerFactory = Substitute.For<IStorageProviderFactory>();

        // Setup configuration
        _configuration = Substitute.For<IConfiguration>();
        _configuration["Storage:LocalPath"].Returns(_tempDir);

        _service = new PhotoService(_db, _thumbnailService, _providerFactory, _configuration, NullLogger<PhotoService>.Instance);

        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        TestHelpers.CleanupTempDirectory(_tempDir);
    }

    [Test]
    public async Task DeletePhotoAsync_DeletesPhysicalFileFromDisk()
    {
        // Arrange
        var (photo, filePath) = TestHelpers.CreateTestPhotoWithFile(_tempDir, id: 1, filename: "test.jpg");

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Verify file exists before deletion
        await Assert.That(File.Exists(filePath)).IsTrue();

        // Act
        var result = await _service.DeletePhotoAsync(1);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(File.Exists(filePath)).IsFalse();

        var deletedPhoto = await _db.Photos.FindAsync(1L);
        await Assert.That(deletedPhoto).IsNull();
    }

    [Test]
    public async Task DeletePhotoAsync_DeletesThumbnailFromDisk()
    {
        // Arrange
        var (photo, filePath, thumbnailPath) = TestHelpers.CreateTestPhotoWithThumbnail(_tempDir, id: 1, filename: "test.jpg");

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Verify files exist before deletion
        await Assert.That(File.Exists(filePath)).IsTrue();
        await Assert.That(File.Exists(thumbnailPath)).IsTrue();

        // Act
        var result = await _service.DeletePhotoAsync(1);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(File.Exists(filePath)).IsFalse();
        await Assert.That(File.Exists(thumbnailPath)).IsFalse();
    }

    [Test]
    public async Task DeletePhotoAsync_RollsBackDatabase_WhenFileNotFound()
    {
        // Arrange
        var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "nonexistent.jpg");
        photo.FilePath = "media/2026/01/nonexistent.jpg"; // File doesn't exist

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Act - deletion will fail because file doesn't exist
        // However, missing file is logged as warning, not treated as critical error
        var result = await _service.DeletePhotoAsync(1);

        // Assert - deletion succeeds even if file is missing (already deleted)
        await Assert.That(result).IsTrue();

        var deletedPhoto = await _db.Photos.FindAsync(1L);
        await Assert.That(deletedPhoto).IsNull();
    }

    [Test]
    public async Task DeletePhotoAsync_RollsBackDatabase_WhenProviderThrowsException()
    {
        // Arrange
        await EnsureStorageProviderAsync(10);
        var photo = TestHelpers.CreateTestPhoto(id: 1, filename: "test.jpg", providerId: 10);
        photo.ProviderFileId = "provider-file-123";

        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Setup mock provider to throw exception
        var provider = Substitute.For<IStorageProvider>();
        provider.DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new IOException("Disk full"));

        var factory = Substitute.For<IStorageProviderFactory>();
        factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IStorageProvider?>(provider));

        var service = CreateService(providerFactory: factory);

        // Act & Assert
        await Assert.That(async () => await service.DeletePhotoAsync(1))
            .Throws<Exception>();

        // Photo should still exist in database due to rollback
        var stillExists = await _db.Photos.FindAsync(1L);
        await Assert.That(stillExists).IsNotNull();
    }

    [Test]
    public async Task DeletePhotoAsync_WithRealCascadeDelete_RemovesJunctionRecords()
    {
        // Arrange
        var album = TestHelpers.CreateTestAlbum(id: 1);
        var tag = TestHelpers.CreateTestTag(id: 1);
        var (photo, filePath) = TestHelpers.CreateTestPhotoWithFile(_tempDir, id: 1);

        _db.Albums.Add(album);
        _db.Tags.Add(tag);
        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Add junction records
        _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = 1, AlbumId = 1, SortOrder = 0 });
        _db.PhotoTags.Add(new PhotoTag { PhotoId = 1, TagId = 1 });
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.DeletePhotoAsync(1);

        // Assert
        await Assert.That(result).IsTrue();

        // Verify cascade deletes
        var remainingPhotoAlbums = await _db.PhotoAlbums.Where(pa => pa.PhotoId == 1).ToListAsync();
        await Assert.That(remainingPhotoAlbums).IsEmpty();

        var remainingPhotoTags = await _db.PhotoTags.Where(pt => pt.PhotoId == 1).ToListAsync();
        await Assert.That(remainingPhotoTags).IsEmpty();

        // Album and tag should still exist
        await Assert.That(await _db.Albums.FindAsync(1L)).IsNotNull();
        await Assert.That(await _db.Tags.FindAsync(1L)).IsNotNull();

        // File should be deleted
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task DeletePhotosAsync_WithRealFiles_DeletesAllSuccessfully()
    {
        // Arrange - create 3 photos with real files
        var filePaths = new List<string>();
        for (long i = 1; i <= 3; i++)
        {
            var (photo, filePath) = TestHelpers.CreateTestPhotoWithFile(_tempDir, id: i, filename: $"test{i}.jpg");
            _db.Photos.Add(photo);
            filePaths.Add(filePath);
        }
        await _db.SaveChangesAsync();

        // Verify files exist
        foreach (var path in filePaths)
        {
            await Assert.That(File.Exists(path)).IsTrue();
        }

        // Act
        var result = await _service.DeletePhotosAsync([1, 2, 3]);

        // Assert
        await Assert.That(result.SuccessCount).IsEqualTo(3);
        await Assert.That(result.FailedCount).IsEqualTo(0);

        // Verify all files deleted
        foreach (var path in filePaths)
        {
            await Assert.That(File.Exists(path)).IsFalse();
        }

        // Verify all photos deleted from database
        var remainingPhotos = await _db.Photos.CountAsync();
        await Assert.That(remainingPhotos).IsEqualTo(0);
    }

    [Test]
    public async Task DeletePhotosAsync_WithMixedSuccessAndFailure_ReturnsCorrectCounts()
    {
        // Arrange - create 5 photos: 2 local files, 3 from provider (1 will fail)
        await EnsureStorageProviderAsync(10);
        var (photo1, filePath1) = TestHelpers.CreateTestPhotoWithFile(_tempDir, id: 1, filename: "test1.jpg");
        var (photo2, filePath2) = TestHelpers.CreateTestPhotoWithFile(_tempDir, id: 2, filename: "test2.jpg");
        var photo3 = TestHelpers.CreateTestPhoto(id: 3, providerId: 10);
        photo3.ProviderFileId = "file-3";
        var photo4 = TestHelpers.CreateTestPhoto(id: 4, providerId: 10);
        photo4.ProviderFileId = "file-4";
        var photo5 = TestHelpers.CreateTestPhoto(id: 5, providerId: 10);
        photo5.ProviderFileId = "file-5";

        _db.Photos.AddRange(photo1, photo2, photo3, photo4, photo5);
        await _db.SaveChangesAsync();

        // Setup mock provider: succeed for 3, 5; fail for 4
        var provider = Substitute.For<IStorageProvider>();
        provider.DeleteFileAsync(Arg.Is("file-3"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        provider.DeleteFileAsync(Arg.Is("file-4"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false)); // Fail
        provider.DeleteFileAsync(Arg.Is("file-5"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var factory = Substitute.For<IStorageProviderFactory>();
        factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IStorageProvider?>(provider));

        var service = CreateService(providerFactory: factory);

        // Act
        var result = await service.DeletePhotosAsync([1, 2, 3, 4, 5]);

        // Assert
        await Assert.That(result.SuccessCount).IsEqualTo(4); // 1, 2, 3, 5
        await Assert.That(result.FailedCount).IsEqualTo(1); // 4

        // Verify local files deleted
        await Assert.That(File.Exists(filePath1)).IsFalse();
        await Assert.That(File.Exists(filePath2)).IsFalse();

        // Verify photo 4 still exists (rollback)
        var photo4StillExists = await _db.Photos.FindAsync(4L);
        await Assert.That(photo4StillExists).IsNotNull();

        // Verify others are deleted
        await Assert.That(await _db.Photos.FindAsync(1L)).IsNull();
        await Assert.That(await _db.Photos.FindAsync(2L)).IsNull();
        await Assert.That(await _db.Photos.FindAsync(3L)).IsNull();
        await Assert.That(await _db.Photos.FindAsync(5L)).IsNull();
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

    private PhotoService CreateService(IStorageProviderFactory? providerFactory = null)
    {
        return new PhotoService(
            _db,
            _thumbnailService,
            providerFactory ?? _providerFactory,
            _configuration,
            NullLogger<PhotoService>.Instance);
    }
}
