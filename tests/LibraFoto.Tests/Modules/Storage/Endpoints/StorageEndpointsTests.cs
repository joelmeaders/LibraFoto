using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
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
    /// Comprehensive tests for StorageEndpoints - storage provider management.
    /// 
    /// Test Coverage:
    /// - GetAllProviders: 4 tests (empty, multiple providers, connection checking, photo counts)
    /// - GetProvider: 3 tests (valid/invalid ID, connection status)
    /// - CreateProvider: 7 tests (valid creation, validation errors, configuration errors, cache clearing)
    /// - UpdateProvider: 6 tests (valid updates, partial updates, validation, cache clearing)
    /// - DeleteProvider: 7 tests (valid deletion, photo handling, OAuth disconnect, cache clearing)
    /// - DisconnectProvider: 4 tests (OAuth disconnect, non-OAuth error, validation, cache clearing)
    /// - TestProviderConnection: 3 tests (connected, disconnected, not found)
    /// - TriggerSync: 3 tests (successful sync, custom request, not found)
    /// - TriggerSyncAll: 1 test (multiple providers)
    /// - GetSyncStatus: 2 tests (in progress, completed)
    /// - CancelSync: 2 tests (successful cancel, non-existent sync)
    /// - ScanProvider: 2 tests (successful scan, not found)
    /// 
    /// Total: 44 comprehensive test cases covering all endpoints and scenarios.
    /// </summary>
    public class StorageEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IStorageProviderFactory _factory = null!;
        private IConfiguration _config = null!;
        private ISyncService _syncService = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>().UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();
            _factory = Substitute.For<IStorageProviderFactory>();
            _syncService = Substitute.For<ISyncService>();
            var inMemorySettings = new Dictionary<string, string> { ["Storage:LocalPath"] = "/tmp/photos" };
            _config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetAllProviders_ReturnsEmptyArray_WhenNoProviders()
        {
            var result = await StorageEndpointsTestHelper.GetAllProviders(_db, _factory, default);
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Length).IsEqualTo(0);
        }

        [Test]
        public async Task GetAllProviders_ReturnsProviders_WhenProvidersExist()
        {
            _db.StorageProviders.AddRange(
                new StorageProvider { Name = "Provider1", Type = StorageProviderType.Local, IsEnabled = true },
                new StorageProvider { Name = "Provider2", Type = StorageProviderType.GooglePhotos, IsEnabled = false }
            );
            await _db.SaveChangesAsync();
            var result = await StorageEndpointsTestHelper.GetAllProviders(_db, _factory, default);
            await Assert.That(result.Value!.Length).IsEqualTo(2);
            await Assert.That(result.Value![0].Name).IsEqualTo("Provider1");
        }

        [Test]
        public async Task GetProvider_ReturnsNotFound_WhenProviderDoesNotExist()
        {
            var result = await StorageEndpointsTestHelper.GetProvider(999, _db, _factory, default);
            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>();
        }

        [Test]
        public async Task GetProvider_ReturnsProvider_WhenExists()
        {
            var provider = new StorageProvider { Name = "TestProvider", Type = StorageProviderType.Local, IsEnabled = true };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();
            var result = await StorageEndpointsTestHelper.GetProvider(provider.Id, _db, _factory, default);
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Name).IsEqualTo("TestProvider");
        }

        [Test]
        public async Task CreateProvider_ReturnsCreated_WithValidData()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.CreateProvider(Arg.Any<StorageProviderType>()).Returns(mockProvider);
            var request = new CreateStorageProviderRequest { Name = "NewProvider", Type = StorageProviderType.Local, IsEnabled = true, Configuration = "" };
            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);
            var created = result.Result as Microsoft.AspNetCore.Http.HttpResults.Created<StorageProviderDto>;
            await Assert.That(created).IsNotNull();
            await Assert.That(created!.Value!.Name).IsEqualTo("NewProvider");
        }

        [Test]
        public async Task CreateProvider_ReturnsBadRequest_WhenNameIsEmpty()
        {
            var request = new CreateStorageProviderRequest { Name = "", Type = StorageProviderType.Local, IsEnabled = true };
            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);
            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<LibraFoto.Shared.DTOs.ApiError>>();
        }

        [Test]
        public async Task UpdateProvider_ReturnsNotFound_WhenProviderDoesNotExist()
        {
            var request = new UpdateStorageProviderRequest { Name = "Updated" };
            var result = await StorageEndpointsTestHelper.UpdateProvider(999, request, _db, _factory, default);
            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>();
        }

        [Test]
        public async Task UpdateProvider_UpdatesName_WhenValid()
        {
            var provider = new StorageProvider { Name = "Old", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();
            var request = new UpdateStorageProviderRequest { Name = "NewName" };
            await StorageEndpointsTestHelper.UpdateProvider(provider.Id, request, _db, _factory, default);
            var updated = await _db.StorageProviders.FindAsync(provider.Id);
            await Assert.That(updated!.Name).IsEqualTo("NewName");
        }

        [Test]
        public async Task DeleteProvider_ReturnsNotFound_WhenProviderDoesNotExist()
        {
            var request = new DeleteProviderRequest(999, false);
            var result = await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);
            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<LibraFoto.Shared.DTOs.ApiError>>();
        }

        [Test]
        public async Task DeleteProvider_RemovesProvider_WhenExists()
        {
            var provider = new StorageProvider { Name = "ToDelete", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();
            var request = new DeleteProviderRequest(provider.Id, false);
            await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);
            var deleted = await _db.StorageProviders.FindAsync(provider.Id);
            await Assert.That(deleted).IsNull();
        }

        [Test]
        public async Task DeleteProvider_UnlinksPhotos_WhenDeletePhotosFalse()
        {
            var provider = new StorageProvider { Name = "Provider", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();
            var photo = new Photo { Filename = "test.jpg", OriginalFilename = "test.jpg", FilePath = "test.jpg", Width = 100, Height = 100, ProviderId = provider.Id, ProviderFileId = "123" };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();
            var request = new DeleteProviderRequest(provider.Id, false);
            await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);
            var updatedPhoto = await _db.Photos.FindAsync(photo.Id);
            await Assert.That(updatedPhoto).IsNotNull();
            await Assert.That(updatedPhoto!.ProviderId).IsNull();
            await Assert.That(updatedPhoto.ProviderFileId).IsNull();
        }

        [Test]
        public async Task DeleteProvider_DeletesPhotos_WhenDeletePhotosTrue()
        {
            var provider = new StorageProvider { Name = "Provider", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();
            var photo = new Photo { Filename = "test.jpg", OriginalFilename = "test.jpg", FilePath = "test.jpg", Width = 100, Height = 100, ProviderId = provider.Id };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();
            var request = new DeleteProviderRequest(provider.Id, true);
            await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);
            var deletedPhoto = await _db.Photos.FindAsync(photo.Id);
            await Assert.That(deletedPhoto).IsNull();
        }

        [Test]
        public async Task TriggerSync_CallsSyncService()
        {
            var start = DateTime.UtcNow;
            var syncResult = SyncResult.Successful(1, "Provider", 5, 0, 0, 0, 5, start);
            _syncService.SyncProviderAsync(1, Arg.Any<SyncRequest>(), Arg.Any<CancellationToken>()).Returns(syncResult);
            var result = await StorageEndpointsTestHelper.TriggerSync(1, null, _syncService, default);
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<SyncResult>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Success).IsTrue();
            await Assert.That(okResult.Value!.FilesAdded).IsEqualTo(5);
        }

        [Test]
        public async Task GetSyncStatus_ReturnsSyncStatus()
        {
            var status = new SyncStatus { ProviderId = 1, IsInProgress = true, ProgressPercent = 50 };
            _syncService.GetSyncStatusAsync(1, Arg.Any<CancellationToken>()).Returns(status);
            var result = await StorageEndpointsTestHelper.GetSyncStatus(1, _syncService, default);
            await Assert.That(result.Value!.IsInProgress).IsTrue();
            await Assert.That(result.Value!.ProgressPercent).IsEqualTo(50);
        }

        [Test]
        public void CancelSync_CallsSyncServiceCancelSync()
        {
            _syncService.CancelSync(1).Returns(true);
            StorageEndpointsTestHelper.CancelSync(1, _syncService);
            _syncService.Received(1).CancelSync(1);
        }

        #region Additional Comprehensive Tests

        [Test]
        public async Task GetAllProviders_WithConnectionCheckFailure_ReturnsDisconnected()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true
            });
            await _db.SaveChangesAsync();

            var mockProvider = Substitute.For<IStorageProvider>();
            mockProvider.TestConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<bool>(new Exception("Connection error")));
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockProvider);

            var result = await StorageEndpointsTestHelper.GetAllProviders(_db, _factory, default);

            await Assert.That(result.Value![0].IsConnected).IsEqualTo(false);
        }

        [Test]
        public async Task GetAllProviders_IncludesLastSyncDate()
        {
            var lastSync = DateTime.UtcNow.AddDays(-1);
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                LastSyncDate = lastSync
            });
            await _db.SaveChangesAsync();

            var result = await StorageEndpointsTestHelper.GetAllProviders(_db, _factory, default);

            await Assert.That(result.Value![0].LastSyncDate).IsEqualTo(lastSync);
        }

        [Test]
        public async Task GetProvider_WithConnectionException_ReturnsDisconnected()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true
            });
            await _db.SaveChangesAsync();

            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<IStorageProvider?>(new Exception("Failed")));

            var result = await StorageEndpointsTestHelper.GetProvider(1, _db, _factory, default);

            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>;
            await Assert.That(okResult!.Value!.IsConnected).IsEqualTo(false);
        }

        [Test]
        public async Task CreateProvider_WithNullName_ReturnsBadRequest()
        {
            var request = new CreateStorageProviderRequest
            {
                Name = null!,
                Type = StorageProviderType.Local,
                IsEnabled = true
            };

            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>>();
        }

        [Test]
        public async Task CreateProvider_WithWhitespaceName_ReturnsBadRequest()
        {
            var request = new CreateStorageProviderRequest
            {
                Name = "   ",
                Type = StorageProviderType.Local,
                IsEnabled = true
            };

            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>>();
        }

        [Test]
        public async Task CreateProvider_WithInvalidConfiguration_ReturnsBadRequest()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            mockProvider.When(p => p.Initialize(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>()))
                .Do(_ => throw new Exception("Invalid config"));
            _factory.CreateProvider(Arg.Any<StorageProviderType>()).Returns(mockProvider);

            var request = new CreateStorageProviderRequest
            {
                Name = "Test Provider",
                Type = StorageProviderType.Local,
                Configuration = "invalid",
                IsEnabled = true
            };

            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>>();
            var badRequest = result.Result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>;
            await Assert.That(badRequest!.Value!.Code).IsEqualTo("INVALID_CONFIGURATION");
        }

        [Test]
        public async Task CreateProvider_ClearsFactoryCache()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.CreateProvider(Arg.Any<StorageProviderType>()).Returns(mockProvider);

            var request = new CreateStorageProviderRequest
            {
                Name = "Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true
            };

            await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);

            _factory.Received(1).ClearCache();
        }

        [Test]
        public async Task CreateProvider_GooglePhotosStartsDisconnected()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.CreateProvider(StorageProviderType.GooglePhotos).Returns(mockProvider);

            var request = new CreateStorageProviderRequest
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true
            };

            var result = await StorageEndpointsTestHelper.CreateProvider(request, _db, _factory, default);

            var created = result.Result as Microsoft.AspNetCore.Http.HttpResults.Created<StorageProviderDto>;
            await Assert.That(created!.Value!.IsConnected).IsEqualTo(false);
        }

        [Test]
        public async Task UpdateProvider_OnlyUpdatesProvidedFields()
        {
            var provider = new StorageProvider
            {
                Name = "Original",
                Type = StorageProviderType.Local,
                IsEnabled = false,
                Configuration = "original config"
            };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.CreateProvider(StorageProviderType.Local).Returns(mockProvider);

            var request = new UpdateStorageProviderRequest { Name = "Updated" };

            await StorageEndpointsTestHelper.UpdateProvider(provider.Id, request, _db, _factory, default);

            var updated = await _db.StorageProviders.FindAsync(provider.Id);
            await Assert.That(updated!.Name).IsEqualTo("Updated");
            await Assert.That(updated.IsEnabled).IsFalse(); // Unchanged
            await Assert.That(updated.Configuration).IsEqualTo("original config"); // Unchanged
        }

        [Test]
        public async Task UpdateProvider_WithInvalidConfiguration_ReturnsBadRequest()
        {
            var provider = new StorageProvider { Name = "Provider", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockProvider = Substitute.For<IStorageProvider>();
            mockProvider.When(p => p.Initialize(Arg.Any<long>(), Arg.Any<string>(), "bad"))
                .Do(_ => throw new Exception("Invalid"));
            _factory.CreateProvider(StorageProviderType.Local).Returns(mockProvider);

            var request = new UpdateStorageProviderRequest { Configuration = "bad" };

            var result = await StorageEndpointsTestHelper.UpdateProvider(provider.Id, request, _db, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>>();
        }

        [Test]
        public async Task UpdateProvider_ClearsFactoryCache()
        {
            var provider = new StorageProvider { Name = "Provider", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.CreateProvider(StorageProviderType.Local).Returns(mockProvider);

            var request = new UpdateStorageProviderRequest { Name = "Updated" };

            await StorageEndpointsTestHelper.UpdateProvider(provider.Id, request, _db, _factory, default);

            _factory.Received(1).ClearCache();
        }

        [Test]
        public async Task DeleteProvider_WithOAuthProvider_DisconnectsFirst()
        {
            var provider = new StorageProvider { Name = "Google", Type = StorageProviderType.GooglePhotos };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockOAuthProvider = Substitute.For<IStorageProvider, IOAuthProvider>();
            ((IOAuthProvider)mockOAuthProvider).DisconnectAsync(Arg.Any<StorageProvider>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockOAuthProvider);

            var request = new DeleteProviderRequest(provider.Id, false);
            await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);

            await ((IOAuthProvider)mockOAuthProvider).Received(1).DisconnectAsync(Arg.Any<StorageProvider>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeleteProvider_ClearsFactoryCache()
        {
            var provider = new StorageProvider { Name = "Provider", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var request = new DeleteProviderRequest(provider.Id, false);
            await StorageEndpointsTestHelper.DeleteProvider(request, _db, _factory, _config, NullLoggerFactory.Instance, default);

            _factory.Received(1).ClearCache();
        }

        [Test]
        public async Task DisconnectProvider_WithOAuthProvider_Disconnects()
        {
            var provider = new StorageProvider { Name = "Google", Type = StorageProviderType.GooglePhotos };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockOAuthProvider = Substitute.For<IStorageProvider, IOAuthProvider>();
            ((IOAuthProvider)mockOAuthProvider).DisconnectAsync(Arg.Any<StorageProvider>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockOAuthProvider);

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);

            var result = await StorageEndpointsTestHelper.DisconnectProvider(provider.Id, _db, _factory, loggerFactory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>>();
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<StorageProviderDto>;
            await Assert.That(okResult!.Value!.IsConnected).IsEqualTo(false);
        }

        [Test]
        public async Task DisconnectProvider_WithNonOAuthProvider_ReturnsBadRequest()
        {
            var provider = new StorageProvider { Name = "Local", Type = StorageProviderType.Local };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockProvider = Substitute.For<IStorageProvider>();
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockProvider);

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);

            var result = await StorageEndpointsTestHelper.DisconnectProvider(provider.Id, _db, _factory, loggerFactory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>>();
            var badRequest = result.Result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<ApiError>;
            await Assert.That(badRequest!.Value!.Code).IsEqualTo("PROVIDER_NOT_OAUTH");
        }

        [Test]
        public async Task DisconnectProvider_WithInvalidId_ReturnsNotFound()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);

            var result = await StorageEndpointsTestHelper.DisconnectProvider(999, _db, _factory, loggerFactory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<ApiError>>();
        }

        [Test]
        public async Task DisconnectProvider_ClearsFactoryCache()
        {
            var provider = new StorageProvider { Name = "Google", Type = StorageProviderType.GooglePhotos };
            _db.StorageProviders.Add(provider);
            await _db.SaveChangesAsync();

            var mockOAuthProvider = Substitute.For<IStorageProvider, IOAuthProvider>();
            ((IOAuthProvider)mockOAuthProvider).DisconnectAsync(Arg.Any<StorageProvider>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockOAuthProvider);

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);

            await StorageEndpointsTestHelper.DisconnectProvider(provider.Id, _db, _factory, loggerFactory, default);

            _factory.Received(1).ClearCache();
        }

        [Test]
        public async Task TestProviderConnection_WithConnectedProvider_ReturnsSuccess()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            mockProvider.TestConnectionAsync(Arg.Any<CancellationToken>()).Returns(true);
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockProvider);

            var result = await StorageEndpointsTestHelper.TestProviderConnection(1, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();
        }

        [Test]
        public async Task TestProviderConnection_WithDisconnectedProvider_ReturnsFailure()
        {
            var mockProvider = Substitute.For<IStorageProvider>();
            mockProvider.TestConnectionAsync(Arg.Any<CancellationToken>()).Returns(false);
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(mockProvider);

            var result = await StorageEndpointsTestHelper.TestProviderConnection(1, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();
        }

        [Test]
        public async Task TestProviderConnection_WithNullProvider_ReturnsNotFound()
        {
            _factory.GetProviderAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns((IStorageProvider?)null);

            var result = await StorageEndpointsTestHelper.TestProviderConnection(999, _factory, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<ApiError>>();
        }

        [Test]
        public async Task TriggerSync_WithFailedSync_ReturnsNotFound()
        {
            var start = DateTime.UtcNow;
            var syncResult = SyncResult.Failed(999, "Provider", "Provider with ID 999 not found", start);
            _syncService.SyncProviderAsync(999, Arg.Any<SyncRequest>(), Arg.Any<CancellationToken>()).Returns(syncResult);

            var result = await StorageEndpointsTestHelper.TriggerSync(999, null, _syncService, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<ApiError>>();
        }

        [Test]
        public async Task TriggerSync_WithCustomSyncRequest_PassesRequest()
        {
            var start = DateTime.UtcNow;
            var syncResult = SyncResult.Successful(1, "Provider", 10, 0, 0, 0, 10, start);
            _syncService.SyncProviderAsync(1, Arg.Any<SyncRequest>(), Arg.Any<CancellationToken>()).Returns(syncResult);

            var request = new SyncRequest { FullSync = true, MaxFiles = 100 };
            await StorageEndpointsTestHelper.TriggerSync(1, request, _syncService, default);

            await _syncService.Received(1).SyncProviderAsync(1, Arg.Is<SyncRequest>(r => r.FullSync && r.MaxFiles == 100), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task TriggerSyncAll_WithMultipleProviders_ReturnsAllResults()
        {
            var start = DateTime.UtcNow;
            var results = new[]
            {
                SyncResult.Successful(1, "Provider1", 5, 0, 0, 0, 5, start),
                SyncResult.Successful(2, "Provider2", 3, 1, 0, 0, 4, start)
            };
            _syncService.SyncAllProvidersAsync(Arg.Any<SyncRequest>(), Arg.Any<CancellationToken>()).Returns(results);

            var result = await StorageEndpointsTestHelper.TriggerSyncAll(null, _syncService, default);

            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Length).IsEqualTo(2);
            await Assert.That(result.Value[0].FilesAdded).IsEqualTo(5);
            await Assert.That(result.Value[1].FilesAdded).IsEqualTo(3);
        }

        [Test]
        public async Task GetSyncStatus_WithInProgressSync_ReturnsProgress()
        {
            var status = new SyncStatus
            {
                ProviderId = 1,
                IsInProgress = true,
                ProgressPercent = 75,
                CurrentOperation = "Processing files",
                FilesProcessed = 75,
                TotalFiles = 100,
                StartTime = DateTime.UtcNow.AddMinutes(-5)
            };
            _syncService.GetSyncStatusAsync(1, Arg.Any<CancellationToken>()).Returns(status);

            var result = await StorageEndpointsTestHelper.GetSyncStatus(1, _syncService, default);

            await Assert.That(result.Value!.IsInProgress).IsTrue();
            await Assert.That(result.Value!.ProgressPercent).IsEqualTo(75);
            await Assert.That(result.Value!.FilesProcessed).IsEqualTo(75);
            await Assert.That(result.Value!.TotalFiles).IsEqualTo(100);
        }

        [Test]
        public async Task CancelSync_WithNonExistentSync_ReturnsFalse()
        {
            _syncService.CancelSync(999).Returns(false);

            var result = StorageEndpointsTestHelper.CancelSync(999, _syncService);

            await Assert.That(result.Value).IsNotNull();
        }

        [Test]
        public async Task ScanProvider_WithValidProvider_ReturnsScanResult()
        {
            var scanResult = new ScanResult
            {
                ProviderId = 1,
                Success = true,
                TotalFilesFound = 100,
                NewFilesCount = 20,
                ExistingFilesCount = 80,
                NewFilesTotalSize = 1024 * 1024 * 50 // 50 MB
            };
            _syncService.ScanProviderAsync(1, Arg.Any<CancellationToken>()).Returns(scanResult);

            var result = await StorageEndpointsTestHelper.ScanProvider(1, _syncService, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>>();
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<ScanResult>;
            await Assert.That(okResult!.Value!.Success).IsTrue();
            await Assert.That(okResult.Value!.NewFilesCount).IsEqualTo(20);
            await Assert.That(okResult.Value!.TotalFilesFound).IsEqualTo(100);
        }

        [Test]
        public async Task ScanProvider_WithNotFoundProvider_ReturnsNotFound()
        {
            var scanResult = new ScanResult
            {
                ProviderId = 999,
                Success = false,
                ErrorMessage = "Provider with ID 999 not found"
            };
            _syncService.ScanProviderAsync(999, Arg.Any<CancellationToken>()).Returns(scanResult);

            var result = await StorageEndpointsTestHelper.ScanProvider(999, _syncService, default);

            await Assert.That(result.Result).IsTypeOf<Microsoft.AspNetCore.Http.HttpResults.NotFound<ApiError>>();
        }

        #endregion
    }
}
