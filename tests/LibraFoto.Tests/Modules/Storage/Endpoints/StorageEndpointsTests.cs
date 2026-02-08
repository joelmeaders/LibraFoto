using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Endpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage.Endpoints
{
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
    }
}
