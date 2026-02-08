using System.Reflection;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage.Endpoints
{
    /// <summary>
    /// Comprehensive tests for UploadEndpoints covering file upload validation,
    /// atomic operations, cleanup, batch uploads, and guest uploads.
    /// </summary>
    public class UploadEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IStorageProviderFactory _providerFactory = null!;
        private IMediaScannerService _mediaScanner = null!;
        private IImageImportService _imageImport = null!;
        private IConfiguration _config = null!;
        private IStorageProvider _mockProvider = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // Setup in-memory SQLite database
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>().UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            // Setup mocks
            _providerFactory = Substitute.For<IStorageProviderFactory>();
            _mediaScanner = Substitute.For<IMediaScannerService>();
            _imageImport = Substitute.For<IImageImportService>();

            // Setup mock storage provider
            _mockProvider = Substitute.For<IStorageProvider>();
            _mockProvider.ProviderId.Returns(1L);
            _mockProvider.ProviderType.Returns(StorageProviderType.Local);
            _mockProvider.DisplayName.Returns("Local Storage");

            // Create user for guest link tests
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = "hash",
                Role = UserRole.Admin
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create provider in database
            var provider = new StorageProvider
            {
                Name = "Local Storage",
                Type = StorageProviderType.Local,
                IsEnabled = true
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            _mockProvider.ProviderId.Returns(provider.Id);
            _providerFactory.GetOrCreateDefaultLocalProviderAsync(Arg.Any<CancellationToken>())
                .Returns(_mockProvider);

            // Setup configuration with temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), $"LibraFotoTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempPath);
            var inMemorySettings = new Dictionary<string, string>
            {
                ["Storage:LocalPath"] = tempPath,
                ["Storage:MaxImportDimension"] = "2560"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();

            // Cleanup temp directories
            var tempPath = _config["Storage:LocalPath"];
            if (!string.IsNullOrEmpty(tempPath) && Directory.Exists(tempPath))
            {
                try
                {
                    Directory.Delete(tempPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region UploadFile Tests

        [Test]
        public async Task UploadFile_ReturnsError_WhenNoFileProvided()
        {
            // Arrange
            IFormFile? file = null;

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file!, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("NO_FILE");
        }

        [Test]
        public async Task UploadFile_ReturnsError_WhenFileIsEmpty()
        {
            // Arrange
            var file = CreateMockFile("test.jpg", 0);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("NO_FILE");
        }

        [Test]
        public async Task UploadFile_ReturnsError_WhenFileTooLarge()
        {
            // Arrange - 101 MB file
            var file = CreateMockFile("large.jpg", 101L * 1024 * 1024);
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("FILE_TOO_LARGE");
            await Assert.That(badRequest.Value.Message).Contains("100 MB");
        }

        [Test]
        public async Task UploadFile_ReturnsError_WhenUnsupportedFileType()
        {
            // Arrange
            var file = CreateMockFile("document.pdf", 1024);
            _mediaScanner.IsSupportedMediaFile("document.pdf").Returns(false);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("UNSUPPORTED_TYPE");
        }

        [Test]
        public async Task UploadFile_SuccessfullyUploadsImage()
        {
            // Arrange
            var file = CreateMockFile("photo.jpg", 50000, "image/jpeg");
            _mediaScanner.IsSupportedMediaFile("photo.jpg").Returns(true);
            _mediaScanner.IsSupportedImage("photo.jpg").Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UploadResult>>();
            var okResult = (Ok<UploadResult>)result.Result;
            await Assert.That(okResult.Value!.Success).IsTrue();
            await Assert.That(okResult.Value.PhotoId).IsNotNull();
            await Assert.That(okResult.Value.FileName).IsNotNull();

            // Verify photo was created in database
            var photo = await _db.Photos.FindAsync(okResult.Value.PhotoId);
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.MediaType).IsEqualTo(MediaType.Photo);
            await Assert.That(photo.Width).IsEqualTo(1920);
            await Assert.That(photo.Height).IsEqualTo(1080);
        }

        [Test]
        [NotInParallel]
        public async Task UploadFile_SuccessfullyUploadsVideo()
        {
            // Arrange
            var file = CreateMockFile("video.mp4", 5000000, "video/mp4");
            _mediaScanner.IsSupportedMediaFile("video.mp4").Returns(true);
            _mediaScanner.IsSupportedImage("video.mp4").Returns(false);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UploadResult>>();
            var okResult = (Ok<UploadResult>)result.Result;
            await Assert.That(okResult.Value!.Success).IsTrue();

            var photo = await _db.Photos.FindAsync(okResult.Value.PhotoId);
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.MediaType).IsEqualTo(MediaType.Video);
        }

        [Test]
        public async Task UploadFile_AssignsToAlbum_WhenAlbumIdProvided()
        {
            // Arrange
            var album = new Album
            {
                Name = "Test Album",
                Description = "Test"
            };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var file = CreateMockFile("photo.jpg", 50000);
            _mediaScanner.IsSupportedMediaFile("photo.jpg").Returns(true);
            _mediaScanner.IsSupportedImage("photo.jpg").Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, album.Id, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UploadResult>>();
            var okResult = (Ok<UploadResult>)result.Result;

            var photoAlbum = await _db.PhotoAlbums
                .FirstOrDefaultAsync(pa => pa.PhotoId == okResult.Value!.PhotoId);
            await Assert.That(photoAlbum).IsNotNull();
            await Assert.That(photoAlbum!.AlbumId).IsEqualTo(album.Id);
        }

        [Test]
        public async Task UploadFile_HandlesImageProcessingFailure()
        {
            // Arrange
            var file = CreateMockFile("corrupted.jpg", 50000);
            _mediaScanner.IsSupportedMediaFile("corrupted.jpg").Returns(true);
            _mediaScanner.IsSupportedImage("corrupted.jpg").Returns(true);

            var importResult = ImageImportResult.Failed("Corrupted image data");
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("UPLOAD_FAILED");

            // Verify cleanup - no photos should be left in database
            var photoCount = await _db.Photos.CountAsync();
            await Assert.That(photoCount).IsEqualTo(0);
        }

        #endregion

        #region UploadBatch Tests

        [Test]
        public async Task UploadBatch_ReturnsError_WhenNoFilesProvided()
        {
            // Arrange
            IFormFileCollection? files = null;

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files!, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("NO_FILES");
        }

        [Test]
        public async Task UploadBatch_ReturnsError_WhenEmptyCollection()
        {
            // Arrange
            var files = new FormFileCollection();

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("NO_FILES");
        }

        [Test]
        public async Task UploadBatch_HandlesEmptyFiles()
        {
            // Arrange
            var files = new FormFileCollection
            {
                CreateMockFile("empty.jpg", 0)
            };

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.TotalFiles).IsEqualTo(1);
            await Assert.That(okResult.Value.SuccessfulUploads).IsEqualTo(0);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(1);
            await Assert.That(okResult.Value.Results[0].Success).IsFalse();
        }

        [Test]
        public async Task UploadBatch_HandlesOversizedFiles()
        {
            // Arrange
            var files = new FormFileCollection
            {
                CreateMockFile("large.jpg", 101L * 1024 * 1024)
            };
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.SuccessfulUploads).IsEqualTo(0);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(1);
            await Assert.That(okResult.Value.Results[0].ErrorMessage).Contains("100 MB");
        }

        [Test]
        public async Task UploadBatch_HandlesUnsupportedFileTypes()
        {
            // Arrange
            var files = new FormFileCollection
            {
                CreateMockFile("document.pdf", 1024)
            };
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(false);

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.FailedUploads).IsEqualTo(1);
            await Assert.That(okResult.Value.Results[0].ErrorMessage).Contains("Unsupported file type");
        }

        [Test]
        public async Task UploadBatch_SuccessfullyUploadsMultipleFiles()
        {
            // Arrange
            var files = new FormFileCollection
            {
                CreateMockFile("photo1.jpg", 50000),
                CreateMockFile("photo2.jpg", 60000),
                CreateMockFile("photo3.jpg", 70000)
            };

            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.TotalFiles).IsEqualTo(3);
            await Assert.That(okResult.Value.SuccessfulUploads).IsEqualTo(3);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(0);
            await Assert.That(okResult.Value.AllSuccessful).IsTrue();

            // Verify all photos created in database
            var photoCount = await _db.Photos.CountAsync();
            await Assert.That(photoCount).IsEqualTo(3);
        }

        [Test]
        public async Task UploadBatch_HandlesMixedValidAndInvalidFiles()
        {
            // Arrange
            var files = new FormFileCollection
            {
                CreateMockFile("valid1.jpg", 50000),
                CreateMockFile("empty.jpg", 0),
                CreateMockFile("toolarge.jpg", 101L * 1024 * 1024),
                CreateMockFile("valid2.jpg", 60000),
                CreateMockFile("document.pdf", 1024)
            };

            _mediaScanner.IsSupportedMediaFile(Arg.Is<string>(f => f.EndsWith(".jpg"))).Returns(true);
            _mediaScanner.IsSupportedMediaFile(Arg.Is<string>(f => f.EndsWith(".pdf"))).Returns(false);
            _mediaScanner.IsSupportedImage(Arg.Is<string>(f => f.EndsWith(".jpg"))).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.TotalFiles).IsEqualTo(5);
            await Assert.That(okResult.Value.SuccessfulUploads).IsEqualTo(2);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(3);
            await Assert.That(okResult.Value.AllSuccessful).IsFalse();
        }

        [Test]
        public async Task UploadBatch_AssignsAllFilesToAlbum()
        {
            // Arrange
            var album = new Album { Name = "Batch Album", Description = "Test" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection
            {
                CreateMockFile("photo1.jpg", 50000),
                CreateMockFile("photo2.jpg", 60000)
            };

            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadBatch(
                files, album.Id, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.SuccessfulUploads).IsEqualTo(2);

            var photoAlbumCount = await _db.PhotoAlbums.CountAsync(pa => pa.AlbumId == album.Id);
            await Assert.That(photoAlbumCount).IsEqualTo(2);
        }

        #endregion

        #region GuestUpload Tests

        [Test]
        public async Task GuestUpload_ReturnsNotFound_WhenLinkDoesNotExist()
        {
            // Arrange
            var files = new FormFileCollection { CreateMockFile("photo.jpg", 50000) };

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "nonexistent", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFound = (NotFound<ApiError>)result.Result;
            await Assert.That(notFound.Value!.Code).IsEqualTo("LINK_NOT_FOUND");
        }

        [Test]
        public async Task GuestUpload_ReturnsBadRequest_WhenLinkExpired()
        {
            // Arrange
            var expiredLink = new GuestLink
            {
                Id = "expired",
                Name = "Expired Link",
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                CurrentUploads = 0,
                CreatedById = 1
            };
            _db.GuestLinks.Add(expiredLink);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection { CreateMockFile("photo.jpg", 50000) };

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "expired", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("LINK_EXPIRED");
        }

        [Test]
        public async Task GuestUpload_ReturnsBadRequest_WhenLinkExhausted()
        {
            // Arrange
            var exhaustedLink = new GuestLink
            {
                Id = "exhausted",
                Name = "Exhausted Link",
                MaxUploads = 5,
                CurrentUploads = 5,
                CreatedById = 1
            };
            _db.GuestLinks.Add(exhaustedLink);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection { CreateMockFile("photo.jpg", 50000) };

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "exhausted", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("LINK_EXHAUSTED");
        }

        [Test]
        public async Task GuestUpload_ReturnsBadRequest_WhenNoFiles()
        {
            // Arrange
            var link = new GuestLink
            {
                Id = "valid",
                Name = "Valid Link",
                CreatedById = 1
            };
            _db.GuestLinks.Add(link);
            await _db.SaveChangesAsync();

            IFormFileCollection? files = null;

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "valid", files!, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("NO_FILES");
        }

        [Test]
        public async Task GuestUpload_SuccessfullyUploadsFilesAndUpdatesCounter()
        {
            // Arrange
            var album = new Album { Name = "Guest Album", Description = "Test" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var link = new GuestLink
            {
                Id = "valid",
                Name = "Valid Link",
                TargetAlbumId = album.Id,
                MaxUploads = 10,
                CurrentUploads = 3,
                CreatedById = 1
            };
            _db.GuestLinks.Add(link);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection
            {
                CreateMockFile("photo1.jpg", 50000),
                CreateMockFile("photo2.jpg", 60000)
            };

            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "valid", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<BatchUploadResult>>();
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.SuccessfulUploads).IsEqualTo(2);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(0);

            // Verify counter updated
            var updatedLink = await _db.GuestLinks.FindAsync("valid");
            await Assert.That(updatedLink!.CurrentUploads).IsEqualTo(5); // 3 + 2
        }

        [Test]
        public async Task GuestUpload_OnlyUpdatesCounterForSuccessfulUploads()
        {
            // Arrange
            var link = new GuestLink
            {
                Id = "partial",
                Name = "Partial Link",
                CurrentUploads = 0,
                CreatedById = 1
            };
            _db.GuestLinks.Add(link);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection
            {
                CreateMockFile("valid.jpg", 50000),
                CreateMockFile("invalid.pdf", 1024) // unsupported
            };

            _mediaScanner.IsSupportedMediaFile(Arg.Is<string>(f => f.EndsWith(".jpg"))).Returns(true);
            _mediaScanner.IsSupportedMediaFile(Arg.Is<string>(f => f.EndsWith(".pdf"))).Returns(false);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "partial", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            var okResult = (Ok<BatchUploadResult>)result.Result;
            await Assert.That(okResult.Value!.SuccessfulUploads).IsEqualTo(1);
            await Assert.That(okResult.Value.FailedUploads).IsEqualTo(1);

            // Counter should only increase by 1
            var updatedLink = await _db.GuestLinks.FindAsync("partial");
            await Assert.That(updatedLink!.CurrentUploads).IsEqualTo(1);
        }

        [Test]
        public async Task GuestUpload_AssignsFilesToTargetAlbum()
        {
            // Arrange
            var targetAlbum = new Album { Name = "Target Album", Description = "Test" };
            _db.Albums.Add(targetAlbum);
            await _db.SaveChangesAsync();

            var link = new GuestLink
            {
                Id = "withalbum",
                Name = "With Album Link",
                TargetAlbumId = targetAlbum.Id,
                CreatedById = 1
            };
            _db.GuestLinks.Add(link);
            await _db.SaveChangesAsync();

            var files = new FormFileCollection { CreateMockFile("photo.jpg", 50000) };
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 48000, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.GuestUpload(
                "withalbum", files, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert
            var okResult = (Ok<BatchUploadResult>)result.Result;
            var photoId = okResult.Value!.Results[0].PhotoId;

            var photoAlbum = await _db.PhotoAlbums
                .FirstOrDefaultAsync(pa => pa.PhotoId == photoId && pa.AlbumId == targetAlbum.Id);
            await Assert.That(photoAlbum).IsNotNull();
        }

        #endregion

        #region GetFile Tests

        [Test]
        public async Task GetFile_ReturnsNotFound_WhenProviderDoesNotExist()
        {
            // Arrange
            _providerFactory.GetProviderAsync(999L, Arg.Any<CancellationToken>())
                .Returns((IStorageProvider?)null);

            // Act
            var result = await UploadEndpointsTestHelper.GetFile(
                999L, "test.jpg", _providerFactory, _mediaScanner);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFound = (NotFound<ApiError>)result.Result;
            await Assert.That(notFound.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        public async Task GetFile_ReturnsNotFound_WhenFileDoesNotExist()
        {
            // Arrange
            var provider = Substitute.For<IStorageProvider>();
            provider.GetFileStreamAsync("missing.jpg", Arg.Any<CancellationToken>())
                .Returns<Stream>(_ => throw new FileNotFoundException("File not found"));

            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Act
            var result = await UploadEndpointsTestHelper.GetFile(
                1L, "missing.jpg", _providerFactory, _mediaScanner);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFound = (NotFound<ApiError>)result.Result;
            await Assert.That(notFound.Value!.Code).IsEqualTo("FILE_NOT_FOUND");
        }

        [Test]
        public async Task GetFile_ReturnsFileStream_WhenFileExists()
        {
            // Arrange
            var provider = Substitute.For<IStorageProvider>();
            var fileContent = new byte[] { 1, 2, 3, 4, 5 };
            var stream = new MemoryStream(fileContent);

            provider.GetFileStreamAsync("photo.jpg", Arg.Any<CancellationToken>())
                .Returns(stream);

            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            _mediaScanner.GetContentType("photo.jpg").Returns("image/jpeg");

            // Act
            var result = await UploadEndpointsTestHelper.GetFile(
                1L, "photo.jpg", _providerFactory, _mediaScanner);

            // Assert
            await Assert.That(result.Result).IsTypeOf<FileStreamHttpResult>();
        }

        #endregion

        #region Security Tests

        [Test]
        public async Task UploadFile_SanitizesFilePath_PreventingDirectoryTraversal()
        {
            // Arrange - attempt path traversal attack
            var file = CreateMockFile("../../../etc/passwd", 1024);
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 100, 100, 1024, false, 100, 100);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert - should succeed but sanitize the path
            await Assert.That(result.Result).IsTypeOf<Ok<UploadResult>>();
            var okResult = (Ok<UploadResult>)result.Result;

            // Verify the stored file path doesn't contain path traversal
            var photo = await _db.Photos.FindAsync(okResult.Value!.PhotoId);
            await Assert.That(photo!.FilePath).DoesNotContain("..");
        }

        [Test]
        public async Task UploadFile_ValidatesMaxFileSize_PreventingDoS()
        {
            // Arrange - exactly at limit (100 MB)
            var file = CreateMockFile("max.jpg", 100L * 1024 * 1024);
            _mediaScanner.IsSupportedMediaFile(Arg.Any<string>()).Returns(true);
            _mediaScanner.IsSupportedImage(Arg.Any<string>()).Returns(true);

            var importResult = ImageImportResult.Successful(
                "/temp/1.jpg", 1920, 1080, 100L * 1024 * 1024, false, 1920, 1080);
            _imageImport.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(importResult);

            // Act
            var result = await UploadEndpointsTestHelper.UploadFile(
                file, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            // Assert - should succeed at exactly 100MB
            await Assert.That(result.Result).IsTypeOf<Ok<UploadResult>>();

            // Now test 1 byte over
            var oversizeFile = CreateMockFile("oversize.jpg", 100L * 1024 * 1024 + 1);
            var result2 = await UploadEndpointsTestHelper.UploadFile(
                oversizeFile, null, _providerFactory, _mediaScanner, _imageImport,
                _config, _db, NullLogger<object>.Instance);

            await Assert.That(result2.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region Helper Methods

        private static IFormFile CreateMockFile(string fileName, long length, string contentType = "application/octet-stream")
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.Length.Returns(length);
            file.ContentType.Returns(contentType);

            // Return a new stream each time OpenReadStream is called
            // This is necessary because the code uses 'await using' which disposes the stream
            file.OpenReadStream().Returns(_ => new MemoryStream(new byte[Math.Min(length, 1024)]));

            return file;
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class UploadEndpointsTestHelper
    {
        public static async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> UploadFile(
            IFormFile file,
            long? albumId,
            IStorageProviderFactory providerFactory,
            IMediaScannerService mediaScanner,
            IImageImportService imageImport,
            IConfiguration configuration,
            LibraFotoDbContext dbContext,
            Microsoft.Extensions.Logging.ILogger<object> logger)
        {
            var method = typeof(UploadEndpoints)
                .GetMethod("UploadFile", BindingFlags.NonPublic | BindingFlags.Static);

            var result = method!.Invoke(null, new object?[]
            {
                file, albumId, providerFactory, mediaScanner, imageImport,
                configuration, dbContext, logger, CancellationToken.None
            });

            return await (Task<Results<Ok<UploadResult>, BadRequest<ApiError>>>)result!;
        }

        public static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>>> UploadBatch(
            IFormFileCollection files,
            long? albumId,
            IStorageProviderFactory providerFactory,
            IMediaScannerService mediaScanner,
            IImageImportService imageImport,
            IConfiguration configuration,
            LibraFotoDbContext dbContext,
            Microsoft.Extensions.Logging.ILogger<object> logger)
        {
            var method = typeof(UploadEndpoints)
                .GetMethod("UploadBatch", BindingFlags.NonPublic | BindingFlags.Static);

            var result = method!.Invoke(null, new object?[]
            {
                files, albumId, providerFactory, mediaScanner, imageImport,
                configuration, dbContext, logger, CancellationToken.None
            });

            return await (Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>>>)result!;
        }

        public static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>> GuestUpload(
            string linkId,
            IFormFileCollection files,
            IStorageProviderFactory providerFactory,
            IMediaScannerService mediaScanner,
            IImageImportService imageImport,
            IConfiguration configuration,
            LibraFotoDbContext dbContext,
            Microsoft.Extensions.Logging.ILogger<object> logger)
        {
            var method = typeof(UploadEndpoints)
                .GetMethod("GuestUpload", BindingFlags.NonPublic | BindingFlags.Static);

            var result = method!.Invoke(null, new object?[]
            {
                linkId, files, providerFactory, mediaScanner, imageImport,
                configuration, dbContext, logger, CancellationToken.None
            });

            return await (Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>>)result!;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound<ApiError>>> GetFile(
            long providerId,
            string fileId,
            IStorageProviderFactory providerFactory,
            IMediaScannerService mediaScanner)
        {
            var method = typeof(UploadEndpoints)
                .GetMethod("GetFile", BindingFlags.NonPublic | BindingFlags.Static);

            var result = method!.Invoke(null, new object[]
            {
                providerId, fileId, providerFactory, mediaScanner, CancellationToken.None
            });

            return await (Task<Results<FileStreamHttpResult, NotFound<ApiError>>>)result!;
        }
    }
}


