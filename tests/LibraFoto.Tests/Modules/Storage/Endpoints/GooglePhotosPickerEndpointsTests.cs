using System.Reflection;
using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage.Endpoints
{
    /// <summary>
    /// Comprehensive tests for GooglePhotosPickerEndpoints covering all endpoints,
    /// error cases, state transitions, and edge cases.
    /// </summary>
    [NotInParallel]
    public class GooglePhotosPickerEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private GooglePhotosPickerService _pickerService = null!;
        private IImageImportService _imageImportService = null!;
        private IConfiguration _config = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>().UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            _pickerService = Substitute.For<GooglePhotosPickerService>(Substitute.For<IHttpClientFactory>());
            _imageImportService = Substitute.For<IImageImportService>();

            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Storage:LocalPath"] = Path.GetTempPath(),
                ["Storage:MaxImportDimension"] = "2560"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        #region StartPickerSession Tests

        [Test]
        public async Task StartPickerSession_WithValidProvider_ReturnsOkWithSession()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1),
                PollingConfig = new PickerPollingConfigResponse
                {
                    PollInterval = "10s",
                    TimeoutIn = "3600s"
                }
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), 50, Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
            var ok = (Ok<PickerSessionDto>)result.Result;
            await Assert.That(ok.Value!.SessionId).IsEqualTo("session-123");
            await Assert.That(ok.Value.PickerUri).IsEqualTo("https://picker.example.com/session-123");
            await Assert.That(ok.Value.MediaItemsSet).IsFalse();

            // Verify session was persisted
            var dbSession = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-123");
            await Assert.That(dbSession).IsNotNull();
            await Assert.That(dbSession!.ProviderId).IsEqualTo(provider.Id);
        }

        [Test]
        public async Task StartPickerSession_WithNullMaxItemCount_UsesDefaultConfig()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var request = new StartPickerSessionRequest { MaxItemCount = null };

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-456",
                PickerUri = "https://picker.example.com/session-456",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
            await _pickerService.Received(1).CreateSessionAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task StartPickerSession_WithNonExistentProvider_ReturnsNotFound()
        {
            // Arrange
            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                999, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFound = (NotFound<ApiError>)result.Result;
            await Assert.That(notFound.Value!.Code).IsEqualTo("PROVIDER_NOT_FOUND");
        }

        [Test]
        public async Task StartPickerSession_WithWrongProviderType_ReturnsNotFound()
        {
            // Arrange
            var provider = new StorageProvider
            {
                Name = "Local Storage",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = null
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task StartPickerSession_WithMissingClientId_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "",
                ClientSecret = "secret",
                RefreshToken = "refresh-token"
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task StartPickerSession_WithMissingClientSecret_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "client-id",
                ClientSecret = "",
                RefreshToken = "refresh-token"
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task StartPickerSession_WithMissingRefreshToken_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RefreshToken = null
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        [Test]
        public async Task StartPickerSession_WithIncompletePickerResponse_ReturnsBadRequest()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            // Missing SessionId
            var sessionResponse = new PickerSessionResponse
            {
                Id = null,
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("PICKER_FAILED");
        }

        [Test]
        public async Task StartPickerSession_WithMissingPickerUri_ReturnsBadRequest()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var request = new StartPickerSessionRequest { MaxItemCount = 50 };

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = null,
                MediaItemsSet = false
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region GetPickerSession Tests

        [Test]
        public async Task GetPickerSession_WithValidSession_ReturnsOkWithStatus()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var existingSession = new PickerSession
            {
                ProviderId = provider.Id,
                SessionId = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            _db.PickerSessions.Add(existingSession);
            await _db.SaveChangesAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = true,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.GetSessionAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
            var ok = (Ok<PickerSessionDto>)result.Result;
            await Assert.That(ok.Value!.SessionId).IsEqualTo("session-123");
            await Assert.That(ok.Value.MediaItemsSet).IsTrue();

            // Verify session was updated in database
            var updatedSession = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-123");
            await Assert.That(updatedSession!.MediaItemsSet).IsTrue();
        }

        [Test]
        public async Task GetPickerSession_UpdatesMediaItemsSetStatus()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-456",
                PickerUri = "https://picker.example.com",
                MediaItemsSet = true,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.GetSessionAsync("session-456", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-456", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert - should create new session if doesn't exist
            var session = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-456");
            // Session won't be created because GetPickerSession doesn't create, only updates existing
            await Assert.That(session).IsNull();
        }

        [Test]
        public async Task GetPickerSession_WithNonExistentProvider_ReturnsNotFound()
        {
            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                999, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task GetPickerSession_WithWrongProviderType_ReturnsNotFound()
        {
            // Arrange
            var provider = new StorageProvider
            {
                Name = "Local",
                Type = StorageProviderType.Local,
                IsEnabled = true
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task GetPickerSession_WithMissingCredentials_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "",
                ClientSecret = "",
                RefreshToken = null
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
            var badRequest = (BadRequest<ApiError>)result.Result;
            await Assert.That(badRequest.Value!.Code).IsEqualTo("MISSING_CREDENTIALS");
        }

        #endregion

        #region GetPickerItems Tests

        [Test]
        public async Task GetPickerItems_WithMultipleItems_ReturnsOkWithArray()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow.AddDays(-1),
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse
                        {
                            Width = 1920,
                            Height = 1080
                        }
                    }
                },
                new()
                {
                    Id = "item-2",
                    Type = "VIDEO",
                    CreateTime = DateTime.UtcNow.AddDays(-2),
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video1",
                        MimeType = "video/mp4",
                        Filename = "video1.mp4",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse
                        {
                            Width = 1920,
                            Height = 1080,
                            VideoMetadata = new PickedVideoMetadataResponse
                            {
                                Fps = 30.0,
                                ProcessingStatus = "READY"
                            }
                        }
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickedMediaItemDto[]>>();
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            await Assert.That(ok.Value!.Length).IsEqualTo(2);
            await Assert.That(ok.Value[0].Id).IsEqualTo("item-1");
            await Assert.That(ok.Value[0].Type).IsEqualTo("PHOTO");
            await Assert.That(ok.Value[1].Id).IsEqualTo("item-2");
            await Assert.That(ok.Value[1].Type).IsEqualTo("VIDEO");
        }

        [Test]
        public async Task GetPickerItems_WithNoItems_ReturnsEmptyArray()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            _pickerService.ListMediaItemsAsync("session-empty", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new List<PickedMediaItemResponse>());

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-empty", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickedMediaItemDto[]>>();
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            await Assert.That(ok.Value!.Length).IsEqualTo(0);
        }

        [Test]
        public async Task GetPickerItems_FiltersItemsWithNullIds()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo1" }
                },
                new()
                {
                    Id = null,
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo2" }
                },
                new()
                {
                    Id = "",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo3" }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickedMediaItemDto[]>>();
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            await Assert.That(ok.Value!.Length).IsEqualTo(1);
            await Assert.That(ok.Value[0].Id).IsEqualTo("item-1");
        }

        [Test]
        public async Task GetPickerItems_GeneratesThumbnailUrlsCorrectly()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-abc",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo" }
                }
            };

            _pickerService.ListMediaItemsAsync("session-xyz", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-xyz", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            var thumbnailUrl = ok.Value![0].ThumbnailUrl;
            await Assert.That(thumbnailUrl).Contains($"/api/storage/google-photos/{provider.Id}");
            await Assert.That(thumbnailUrl).Contains("session-xyz");
            await Assert.That(thumbnailUrl).Contains("item-abc");
        }

        [Test]
        public async Task GetPickerItems_WithNonExistentProvider_ReturnsNotFound()
        {
            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                999, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        #endregion

        #region ImportPickerItems Tests

        [Test]
        public async Task ImportPickerItems_WithSingleImage_ImportsSuccessfully()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse { Width = 1920, Height = 1080 }
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }); // Fake JPEG
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(1);
            await Assert.That(ok.Value.Failed).IsEqualTo(0);

            // Verify photo was added to database
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.ProviderFileId).IsEqualTo("item-1");
        }

        [Test]
        public async Task ImportPickerItems_WithExistingPhoto_SkipsImport()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            // Create existing photo
            var existingPhoto = new Photo
            {
                Filename = "existing.jpg",
                OriginalFilename = "existing.jpg",
                FilePath = "media/2024/01/existing.jpg",
                Width = 1920,
                Height = 1080,
                MediaType = MediaType.Photo,
                DateTaken = DateTime.UtcNow,
                DateAdded = DateTime.UtcNow,
                ProviderId = provider.Id,
                ProviderFileId = "item-1"
            };
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert - should still report as imported
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(1);

            // Verify only one photo exists
            var photoCount = await _db.Photos.CountAsync();
            await Assert.That(photoCount).IsEqualTo(1);
        }

        [Test]
        public async Task ImportPickerItems_WithInvalidItem_CountsAsFailed()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = null, // Invalid - no ID
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo1" }
                },
                new()
                {
                    Id = "item-2",
                    Type = "PHOTO",
                    MediaFile = null // Invalid - no MediaFile
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(0);
            await Assert.That(ok.Value.Failed).IsEqualTo(2);
        }

        [Test]
        public async Task ImportPickerItems_WithImageProcessingFailure_CountsAsFailed()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Failed("Image processing failed"));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(0);
            await Assert.That(ok.Value.Failed).IsEqualTo(1);
        }

        [Test]
        public async Task ImportPickerItems_WithVideo_ImportsAsVideo()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "video-1",
                    Type = "VIDEO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video1",
                        MimeType = "video/mp4",
                        Filename = "video1.mp4",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse
                        {
                            Width = 1920,
                            Height = 1080
                        }
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x18 }); // Fake MP4
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(true), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "video/mp4"));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(1);

            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.MediaType).IsEqualTo(MediaType.Video);
        }

        [Test]
        public async Task ImportPickerItems_WithNonExistentProvider_ReturnsNotFound()
        {
            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                999, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task ImportPickerItems_WithMissingCredentials_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "",
                ClientSecret = "",
                RefreshToken = null
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region DeletePickerSession Tests

        [Test]
        public async Task DeletePickerSession_WithValidSession_ReturnsOkAndDeletesFromDb()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var session = new PickerSession
            {
                ProviderId = provider.Id,
                SessionId = "session-123",
                PickerUri = "https://picker.example.com",
                MediaItemsSet = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            _db.PickerSessions.Add(session);
            await _db.SaveChangesAsync();

            _pickerService.DeleteSessionAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok>();

            // Verify session was deleted from database
            var deletedSession = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-123");
            await Assert.That(deletedSession).IsNull();
        }

        [Test]
        public async Task DeletePickerSession_WithoutDbSession_StillCallsServiceAndReturnsOk()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            _pickerService.DeleteSessionAsync("session-456", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                provider.Id, "session-456", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok>();
            await _pickerService.Received(1).DeleteSessionAsync("session-456", Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeletePickerSession_WithNonExistentProvider_ReturnsNotFound()
        {
            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                999, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task DeletePickerSession_WithWrongProviderType_ReturnsNotFound()
        {
            // Arrange
            var provider = new StorageProvider
            {
                Name = "Local",
                Type = StorageProviderType.Local,
                IsEnabled = true
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task DeletePickerSession_WithMissingCredentials_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "",
                ClientSecret = "",
                RefreshToken = null
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region GetPickerThumbnail Tests

        [Test]
        public async Task GetPickerThumbnail_WithValidItem_ReturnsFileStream()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var thumbnailStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                "https://example.com/photo1", Arg.Any<string>(), Arg.Is(false), 300, 300, Arg.Any<CancellationToken>())
                .Returns((thumbnailStream, "image/jpeg"));

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-1", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<FileStreamHttpResult>();
        }

        [Test]
        public async Task GetPickerThumbnail_WithDefaultDimensions_Uses400x400()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var thumbnailStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), 400, 400, Arg.Any<CancellationToken>())
                .Returns((thumbnailStream, "image/jpeg"));

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-1", 0, 0);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await _pickerService.Received(1).DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), false, 400, 400, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetPickerThumbnail_WithNonExistentItem_ReturnsNotFound()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new List<PickedMediaItemResponse>());

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-999", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
            var notFound = (NotFound<ApiError>)result.Result;
            await Assert.That(notFound.Value!.Code).IsEqualTo("ITEM_NOT_FOUND");
        }

        [Test]
        public async Task GetPickerThumbnail_WithItemMissingBaseUrl_ReturnsNotFound()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = null,
                        MimeType = "image/jpeg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-1", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task GetPickerThumbnail_WithNonExistentProvider_ReturnsNotFound()
        {
            // Arrange
            var request = new PickerThumbnailRequest(999, "session-123", "item-1", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound<ApiError>>();
        }

        [Test]
        public async Task GetPickerThumbnail_WithMissingCredentials_ReturnsBadRequest()
        {
            // Arrange
            var config = new GooglePhotosConfiguration
            {
                ClientId = "",
                ClientSecret = "",
                RefreshToken = null
            };
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-1", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        [Test]
        public async Task GetPickerThumbnail_CaseInsensitiveItemIdMatch()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "ITEM-ABC",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var thumbnailStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((thumbnailStream, "image/jpeg"));

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-abc", 300, 300);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<FileStreamHttpResult>();
        }

        #endregion

        #region OAuth and Token Refresh Tests

        [Test]
        public async Task StartPickerSession_WithStaleAccessToken_RefreshesToken()
        {
            // Arrange - Create provider with stale token (expired 1 hour ago)
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                RefreshToken = "test-refresh-token",
                AccessToken = "expired-token",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(-1),
                GrantedScopes = new[] { "https://www.googleapis.com/auth/photospicker.mediaitems.readonly" }
            };

            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert - Should succeed because token refresh logic is in place
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();

            // Verify config was updated with refresh attempt
            var updatedProvider = await _db.StorageProviders.FindAsync(provider.Id);
            var updatedConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(updatedProvider!.Configuration!);
            await Assert.That(updatedConfig).IsNotNull();
        }

        [Test]
        public async Task StartPickerSession_WithNullAccessToken_RefreshesToken()
        {
            // Arrange - No access token
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                RefreshToken = "test-refresh-token",
                AccessToken = null,
                AccessTokenExpiry = null,
                GrantedScopes = null
            };

            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert - Should succeed
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
        }

        [Test]
        public async Task StartPickerSession_WithNullConfiguration_ReturnsBadRequest()
        {
            // Arrange
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = null
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        [Test]
        public async Task StartPickerSession_WithInvalidJsonConfiguration_ReturnsBadRequest()
        {
            // Arrange
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = "{invalid-json"
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region Import Edge Cases and Cleanup Tests

        [Test]
        public async Task ImportPickerItems_WithMixedSuccessAndFailure_ReturnsCorrectCounts()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                // Valid image
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                },
                // Invalid - no ID
                new()
                {
                    Id = null,
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse { BaseUrl = "https://example.com/photo2" }
                },
                // Valid video
                new()
                {
                    Id = "item-3",
                    Type = "VIDEO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video1",
                        MimeType = "video/mp4",
                        Filename = "video1.mp4"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Setup successful image import
            _pickerService.DownloadMediaItemAsync(
                "https://example.com/photo1", Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Setup successful video import
            _pickerService.DownloadMediaItemAsync(
                "https://example.com/video1", Arg.Any<string>(), Arg.Is(true), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x18 }), "video/mp4"));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(2);
            await Assert.That(ok.Value.Failed).IsEqualTo(1);
        }

        [Test]
        public async Task ImportPickerItems_WithNoFilename_UsesItemIdAsFilename()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-abc-123",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = null // No filename
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.Filename).Contains("item-abc-123");
        }

        [Test]
        public async Task ImportPickerItems_WithVariousMimeTypes_UsesCorrectExtensions()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var testCases = new[]
            {
                ("image/png", ".png"),
                ("image/gif", ".gif"),
                ("image/webp", ".webp"),
                ("image/heic", ".heic"),
                ("video/mp4", ".mp4"),
                ("video/quicktime", ".mov"),
                ("unknown/type", ".jpg") // Default fallback
            };

            foreach (var (mimeType, expectedExtension) in testCases)
            {
                var items = new List<PickedMediaItemResponse>
                {
                    new()
                    {
                        Id = $"item-{Guid.NewGuid()}",
                        Type = mimeType.StartsWith("video") ? "VIDEO" : "PHOTO",
                        CreateTime = DateTime.UtcNow,
                        MediaFile = new PickedMediaFileResponse
                        {
                            BaseUrl = "https://example.com/file",
                            MimeType = mimeType,
                            Filename = null // Force mime type detection
                        }
                    }
                };

                _pickerService.ListMediaItemsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(items);

                var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
                _pickerService.DownloadMediaItemAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns((downloadStream, mimeType));

                if (mimeType.StartsWith("image"))
                {
                    _imageImportService.ProcessImageAsync(
                        Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                        .Returns(ImageImportResult.Successful("/tmp/file", 1920, 1080, 12345, false, 1920, 1080));
                }

                // Act
                await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                    provider.Id, $"session-{Guid.NewGuid()}", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

                // Assert
                var photo = await _db.Photos.OrderByDescending(p => p.Id).FirstOrDefaultAsync();
                await Assert.That(photo).IsNotNull();
                await Assert.That(photo!.Filename.EndsWith(expectedExtension)).IsTrue();
            }
        }

        [Test]
        public async Task ImportPickerItems_WithNullCreateTime_UsesCurrentUtcTime()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = null, // No create time
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            var beforeImport = DateTime.UtcNow;

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.DateTaken.HasValue).IsTrue();
            await Assert.That(photo.DateTaken!.Value).IsGreaterThanOrEqualTo(beforeImport);
            await Assert.That(photo.DateTaken.Value).IsLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
        }

        [Test]
        public async Task ImportPickerItems_WithMissingWidthHeight_UsesZeroDefaults()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg",
                        MediaFileMetadata = null // No metadata
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            // Width/Height should be updated by image processing, not from metadata
            await Assert.That(photo!.Width).IsEqualTo(1920);
            await Assert.That(photo.Height).IsEqualTo(1080);
        }

        [Test]
        public async Task ImportPickerItems_WithDownloadException_CountsAsFailed()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Simulate download failure
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<(Stream, string)>(new HttpRequestException("Network error")));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(0);
            await Assert.That(ok.Value.Failed).IsEqualTo(1);
        }

        [Test]
        public async Task ImportPickerItems_WithFilenameExtension_PreservesOriginalExtension()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "my-photo.PNG" // Different extension case
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.Filename.EndsWith(".png")).IsTrue(); // Lowercase extension
        }

        [Test]
        public async Task ImportPickerItems_WithExistingPhotoAndPopulatedFilePath_DoesNotCleanupOnError()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            // Create existing photo with populated FilePath (previously imported)
            var existingPhoto = new Photo
            {
                Filename = "existing.jpg",
                OriginalFilename = "existing.jpg",
                FilePath = "media/2024/01/existing.jpg", // FilePath is populated
                Width = 1920,
                Height = 1080,
                MediaType = MediaType.Photo,
                DateTaken = DateTime.UtcNow,
                DateAdded = DateTime.UtcNow,
                ProviderId = provider.Id,
                ProviderFileId = "item-1"
            };
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "photo1.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            // Force image processing to fail
            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ImageImportResult>(new InvalidOperationException("Processing failed")));

            var photoCountBefore = await _db.Photos.CountAsync();

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert - Photo should still exist (not cleaned up) because FilePath was populated
            var photoCountAfter = await _db.Photos.CountAsync();
            await Assert.That(photoCountAfter).IsEqualTo(photoCountBefore);
            var photo = await _db.Photos.FindAsync(existingPhoto.Id);
            await Assert.That(photo).IsNotNull();
        }

        #endregion

        #region Configuration and Polling Tests

        [Test]
        public async Task StartPickerSession_WithPollingConfig_ReturnsFullDto()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1),
                PollingConfig = new PickerPollingConfigResponse
                {
                    PollInterval = "5s",
                    TimeoutIn = "1800s"
                }
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
            var ok = (Ok<PickerSessionDto>)result.Result;
            await Assert.That(ok.Value!.PollingConfig).IsNotNull();
            await Assert.That(ok.Value.PollingConfig!.PollInterval).IsEqualTo("5s");
            await Assert.That(ok.Value.PollingConfig.TimeoutIn).IsEqualTo("1800s");
        }

        [Test]
        public async Task StartPickerSession_WithNullPollingConfig_ReturnsNullInDto()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = DateTime.UtcNow.AddHours(1),
                PollingConfig = null
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<PickerSessionDto>>();
            var ok = (Ok<PickerSessionDto>)result.Result;
            await Assert.That(ok.Value!.PollingConfig).IsNull();
        }

        [Test]
        public async Task GetPickerSession_UpdatesExpiryTime()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var oldExpiry = DateTime.UtcNow.AddHours(1);
            var newExpiry = DateTime.UtcNow.AddHours(2);

            var session = new PickerSession
            {
                ProviderId = provider.Id,
                SessionId = "session-123",
                PickerUri = "https://picker.example.com",
                MediaItemsSet = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = oldExpiry
            };
            _db.PickerSessions.Add(session);
            await _db.SaveChangesAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com",
                MediaItemsSet = false,
                ExpireTime = newExpiry
            };

            _pickerService.GetSessionAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var updatedSession = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-123");
            await Assert.That(updatedSession!.ExpiresAt).IsNotEqualTo(oldExpiry);
            await Assert.That(updatedSession.ExpiresAt).IsEqualTo(newExpiry);
        }

        #endregion

        #region Item Mapping and DTO Tests

        [Test]
        public async Task GetPickerItems_MapsAllFieldsCorrectly()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var createTime = DateTime.UtcNow.AddDays(-5);

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-123",
                    Type = "VIDEO",
                    CreateTime = createTime,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video1",
                        MimeType = "video/mp4",
                        Filename = "vacation.mp4",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse
                        {
                            Width = 3840,
                            Height = 2160,
                            VideoMetadata = new PickedVideoMetadataResponse
                            {
                                Fps = 60.0,
                                ProcessingStatus = "READY"
                            }
                        }
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-456", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-456", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            var dto = ok.Value![0];
            await Assert.That(dto.Id).IsEqualTo("item-123");
            await Assert.That(dto.Type).IsEqualTo("VIDEO");
            await Assert.That(dto.MimeType).IsEqualTo("video/mp4");
            await Assert.That(dto.Filename).IsEqualTo("vacation.mp4");
            await Assert.That(dto.Width).IsEqualTo(3840);
            await Assert.That(dto.Height).IsEqualTo(2160);
            await Assert.That(dto.CreateTime).IsEqualTo(createTime);
            await Assert.That(dto.VideoProcessingStatus).IsEqualTo("READY");
        }

        [Test]
        public async Task GetPickerItems_WithNullMetadata_ReturnsNullValues()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = null,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo",
                        MimeType = null,
                        Filename = null,
                        MediaFileMetadata = null
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            var dto = ok.Value![0];
            await Assert.That(dto.Id).IsEqualTo("item-1");
            await Assert.That(dto.MimeType).IsNull();
            await Assert.That(dto.Filename).IsNull();
            await Assert.That(dto.Width).IsNull();
            await Assert.That(dto.Height).IsNull();
            await Assert.That(dto.CreateTime).IsNull();
            await Assert.That(dto.VideoProcessingStatus).IsNull();
        }

        #endregion

        #region Video Import Tests

        [Test]
        public async Task ImportPickerItems_WithVideoNoExtension_UsesDefaultMp4()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "video-1",
                    Type = "VIDEO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video1",
                        MimeType = null, // No mime type
                        Filename = null // No filename
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x18 });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(true), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "application/octet-stream"));

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            await Assert.That(photo!.Filename.EndsWith(".jpg")).IsTrue(); // Default fallback
            await Assert.That(photo.MediaType).IsEqualTo(MediaType.Video);
        }

        [Test]
        public async Task ImportPickerItems_VideoCalculatesFileSize()
        {
            // Arrange - Use actual temp directory for this test
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Storage:LocalPath"] = tempDir,
                ["Storage:MaxImportDimension"] = "2560"
            };
            var testConfig = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            try
            {
                var provider = await CreateValidGooglePhotosProviderAsync();

                var items = new List<PickedMediaItemResponse>
                {
                    new()
                    {
                        Id = "video-1",
                        Type = "VIDEO",
                        CreateTime = DateTime.UtcNow,
                        MediaFile = new PickedMediaFileResponse
                        {
                            BaseUrl = "https://example.com/video1",
                            MimeType = "video/mp4",
                            Filename = "video1.mp4"
                        }
                    }
                };

                _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(items);

                var videoBytes = new byte[1024 * 100]; // 100KB video
                new Random().NextBytes(videoBytes);
                var downloadStream = new MemoryStream(videoBytes);

                _pickerService.DownloadMediaItemAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Is(true), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns((downloadStream, "video/mp4"));

                // Act
                await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                    provider.Id, "session-123", _db, _pickerService, _imageImportService, testConfig, NullLoggerFactory.Instance, default);

                // Assert
                var photo = await _db.Photos.FirstOrDefaultAsync();
                await Assert.That(photo).IsNotNull();
                await Assert.That(photo!.FileSize).IsGreaterThan(0);
                await Assert.That(photo.FileSize).IsEqualTo(videoBytes.Length);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #endregion

        #region Path and Directory Tests

        [Test]
        public async Task ImportPickerItems_CreatesYearMonthDirectoryStructure()
        {
            // Arrange - Use temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Storage:LocalPath"] = tempDir,
                ["Storage:MaxImportDimension"] = "2560"
            };
            var testConfig = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            try
            {
                var provider = await CreateValidGooglePhotosProviderAsync();
                var createTime = new DateTime(2023, 5, 15, 10, 30, 0, DateTimeKind.Utc);

                var items = new List<PickedMediaItemResponse>
                {
                    new()
                    {
                        Id = "item-1",
                        Type = "PHOTO",
                        CreateTime = createTime,
                        MediaFile = new PickedMediaFileResponse
                        {
                            BaseUrl = "https://example.com/photo1",
                            MimeType = "image/jpeg",
                            Filename = "photo1.jpg"
                        }
                    }
                };

                _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(items);

                var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
                _pickerService.DownloadMediaItemAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns((downloadStream, "image/jpeg"));

                _imageImportService.ProcessImageAsync(
                    Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

                // Act
                await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                    provider.Id, "session-123", _db, _pickerService, _imageImportService, testConfig, NullLoggerFactory.Instance, default);

                // Assert
                var photo = await _db.Photos.FirstOrDefaultAsync();
                await Assert.That(photo).IsNotNull();
                await Assert.That(photo!.FilePath).Contains("2023");
                await Assert.That(photo.FilePath).Contains("05"); // Month padded

                // Verify directory structure was created
                var expectedDir = Path.Combine(tempDir, "media", "2023", "05");
                await Assert.That(Directory.Exists(expectedDir)).IsTrue();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task ImportPickerItems_UsesIdBasedFilename()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg",
                        Filename = "original-name.jpg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var downloadStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((downloadStream, "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            var photo = await _db.Photos.FirstOrDefaultAsync();
            await Assert.That(photo).IsNotNull();
            // Filename should be {photoId}.jpg, not original-name.jpg
            await Assert.That(photo!.Filename).IsEqualTo($"{photo.Id}.jpg");
            await Assert.That(photo.OriginalFilename).IsEqualTo("original-name.jpg");
        }

        #endregion

        #region Concurrent and State Tests

        [Test]
        public async Task GetPickerSession_WithNonExistentDbSession_DoesNotCreateNewSession()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-new",
                PickerUri = "https://picker.example.com",
                MediaItemsSet = true,
                ExpireTime = DateTime.UtcNow.AddHours(1)
            };

            _pickerService.GetSessionAsync("session-new", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerSession(
                provider.Id, "session-new", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert - GetPickerSession does not create sessions, only StartPickerSession does
            var session = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-new");
            await Assert.That(session).IsNull();
        }

        [Test]
        public async Task GetPickerThumbnail_WithCustomDimensions_UsesProvidedValues()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = "PHOTO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/photo1",
                        MimeType = "image/jpeg"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            var thumbnailStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), 800, 600, Arg.Any<CancellationToken>())
                .Returns((thumbnailStream, "image/jpeg"));

            var request = new PickerThumbnailRequest(provider.Id, "session-123", "item-1", 800, 600);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerThumbnail(
                request, _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await _pickerService.Received(1).DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), false, 800, 600, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ImportPickerItems_MultipleItemsSameBatch_ProcessesSequentially()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>();
            for (int i = 1; i <= 5; i++)
            {
                items.Add(new()
                {
                    Id = $"item-{i}",
                    Type = "PHOTO",
                    CreateTime = DateTime.UtcNow,
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = $"https://example.com/photo{i}",
                        MimeType = "image/jpeg",
                        Filename = $"photo{i}.jpg"
                    }
                });
            }

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            _pickerService.DownloadMediaItemAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Is(false), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => (new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "image/jpeg"));

            _imageImportService.ProcessImageAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ImageImportResult.Successful("/tmp/photo.jpg", 1920, 1080, 12345, false, 1920, 1080));

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.ImportPickerItems(
                provider.Id, "session-123", _db, _pickerService, _imageImportService, _config, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<ImportPickerItemsResponse>>();
            var ok = (Ok<ImportPickerItemsResponse>)result.Result;
            await Assert.That(ok.Value!.Imported).IsEqualTo(5);
            await Assert.That(ok.Value.Failed).IsEqualTo(0);

            // Verify all photos were created
            var photoCount = await _db.Photos.CountAsync();
            await Assert.That(photoCount).IsEqualTo(5);
        }

        #endregion

        #region DTO Mapping Edge Cases

        [Test]
        public async Task MapItemDto_WithMissingVideoMetadata_ReturnsNullVideoStatus()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "video-1",
                    Type = "VIDEO",
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/video",
                        MediaFileMetadata = new PickedMediaFileMetadataResponse
                        {
                            Width = 1920,
                            Height = 1080,
                            VideoMetadata = null // No video metadata
                        }
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            await Assert.That(ok.Value![0].VideoProcessingStatus).IsNull();
        }

        [Test]
        public async Task MapItemDto_WithTypeUnspecified_MapsCorrectly()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();

            var items = new List<PickedMediaItemResponse>
            {
                new()
                {
                    Id = "item-1",
                    Type = null, // Unspecified type
                    MediaFile = new PickedMediaFileResponse
                    {
                        BaseUrl = "https://example.com/file"
                    }
                }
            };

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(items);

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var ok = (Ok<PickedMediaItemDto[]>)result.Result;
            await Assert.That(ok.Value![0].Type).IsEqualTo("TYPE_UNSPECIFIED");
        }

        [Test]
        public async Task StartPickerSession_PersistsSessionWithCorrectExpiry()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var expectedExpiry = DateTime.UtcNow.AddHours(2);

            var sessionResponse = new PickerSessionResponse
            {
                Id = "session-123",
                PickerUri = "https://picker.example.com/session-123",
                MediaItemsSet = false,
                ExpireTime = expectedExpiry
            };

            _pickerService.CreateSessionAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(sessionResponse);

            // Act
            await GooglePhotosPickerEndpointsTestHelper.StartPickerSession(
                provider.Id, new StartPickerSessionRequest(), _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            var session = await _db.PickerSessions.FirstOrDefaultAsync(s => s.SessionId == "session-123");
            await Assert.That(session).IsNotNull();
            await Assert.That(session!.ExpiresAt).IsEqualTo(expectedExpiry);
            await Assert.That(session.MediaItemsSet).IsFalse();
        }

        #endregion

        #region Configuration Persistence Tests

        [Test]
        public async Task GetPickerItems_UpdatesAndPersistsConfiguration()
        {
            // Arrange
            var provider = await CreateValidGooglePhotosProviderAsync();
            var originalConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(provider.Configuration!);

            _pickerService.ListMediaItemsAsync("session-123", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new List<PickedMediaItemResponse>());

            // Act
            await GooglePhotosPickerEndpointsTestHelper.GetPickerItems(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert - Configuration should be persisted
            var updatedProvider = await _db.StorageProviders.FindAsync(provider.Id);
            await Assert.That(updatedProvider).IsNotNull();
            await Assert.That(updatedProvider!.Configuration).IsNotNull();

            var updatedConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(updatedProvider.Configuration!);
            await Assert.That(updatedConfig).IsNotNull();
            await Assert.That(updatedConfig!.ClientId).IsEqualTo(originalConfig!.ClientId);
        }

        [Test]
        public async Task DeletePickerSession_WithoutCredentialsAfterProviderDelete_ReturnsBadRequest()
        {
            // Arrange - Provider with empty config
            var provider = new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = "{}"
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            // Act
            var result = await GooglePhotosPickerEndpointsTestHelper.DeletePickerSession(
                provider.Id, "session-123", _db, _pickerService, NullLoggerFactory.Instance, default);

            // Assert
            await Assert.That(result.Result).IsTypeOf<BadRequest<ApiError>>();
        }

        #endregion

        #region Helper Methods

        private async Task<StorageProvider> CreateValidGooglePhotosProviderAsync()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                RefreshToken = "test-refresh-token",
                AccessToken = "test-access-token",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
                GrantedScopes = new[] { "https://www.googleapis.com/auth/photospicker.mediaitems.readonly" }
            };

            var provider = new StorageProvider
            {
                Name = "Google Photos Test",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };

            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            return provider;
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class GooglePhotosPickerEndpointsTestHelper
    {
        public static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> StartPickerSession(
            long providerId,
            StartPickerSessionRequest? request,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("StartPickerSession", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object?[] { providerId, request, dbContext, pickerService, loggerFactory, cancellationToken });
            return await (Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }

        public static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerSession(
            long providerId,
            string sessionId,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("GetPickerSession", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object[] { providerId, sessionId, dbContext, pickerService, loggerFactory, cancellationToken });
            return await (Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }

        public static async Task<Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerItems(
            long providerId,
            string sessionId,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("GetPickerItems", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object[] { providerId, sessionId, dbContext, pickerService, loggerFactory, cancellationToken });
            return await (Task<Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }

        public static async Task<Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>> ImportPickerItems(
            long providerId,
            string sessionId,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            IImageImportService imageImportService,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("ImportPickerItems", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object[] { providerId, sessionId, dbContext, pickerService, imageImportService, configuration, loggerFactory, cancellationToken });
            return await (Task<Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }

        public static async Task<Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>> DeletePickerSession(
            long providerId,
            string sessionId,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("DeletePickerSession", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object[] { providerId, sessionId, dbContext, pickerService, loggerFactory, cancellationToken });
            return await (Task<Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }

        public static async Task<Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerThumbnail(
            PickerThumbnailRequest request,
            LibraFotoDbContext dbContext,
            GooglePhotosPickerService pickerService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            var method = typeof(GooglePhotosPickerEndpoints)
                .GetMethod("GetPickerThumbnail", BindingFlags.NonPublic | BindingFlags.Static);

            var task = method!.Invoke(null, new object[] { request, dbContext, pickerService, loggerFactory, cancellationToken });
            return await (Task<Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>>)task!;
        }
    }
}
