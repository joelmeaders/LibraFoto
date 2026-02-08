using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    public class SyncServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IStorageProviderFactory _providerFactory = null!;
        private IServiceProvider _serviceProvider = null!;
        private SyncService _syncService = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection).EnableDetailedErrors().Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            _providerFactory = Substitute.For<IStorageProviderFactory>();

            // Set up scoped service provider so SyncService can resolve LibraFotoDbContext
            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.GetService(typeof(LibraFotoDbContext)).Returns(_db);
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            scopeFactory.CreateScope().Returns(scope);
            _serviceProvider = Substitute.For<IServiceProvider>();
            _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

            _syncService = new SyncService(
                _providerFactory,
                _serviceProvider,
                NullLogger<SyncService>.Instance);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        // --- SyncProviderAsync Tests ---

        [Test]
        public async Task SyncProviderAsync_ReturnsFailedResult_WhenProviderNotFound()
        {
            // Arrange
            _providerFactory.GetProviderAsync(99L, Arg.Any<CancellationToken>())
                .Returns((IStorageProvider?)null);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(99L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).IsNotNull();
            await Assert.That(result.ErrorMessage!).Contains("not found");
        }

        [Test]
        public async Task SyncProviderAsync_AddsNewFilesToDatabase()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");
            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1.jpg", "photo1.jpg", MediaType.Photo, 2000),
                TestHelpers.CreateTestStorageFileInfo("file2.jpg", "photo2.jpg", MediaType.Photo, 3000)
            };
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Seed the storage provider entity
            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(2);
            await Assert.That(result.FilesSkipped).IsEqualTo(0);

            var photosInDb = await _db.Photos.Where(p => p.ProviderId == 1L).CountAsync();
            await Assert.That(photosInDb).IsEqualTo(2);
        }

        [Test]
        public async Task SyncProviderAsync_SkipsExistingFiles_WhenSkipExistingIsTrue()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            // Pre-seed a photo that already exists
            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            var existingPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "existing.jpg", providerId: 1L);
            existingPhoto.ProviderFileId = "existing-file-id";
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("existing-file-id", "existing.jpg", MediaType.Photo, 2000),
                TestHelpers.CreateTestStorageFileInfo("new-file-id", "new.jpg", MediaType.Photo, 3000)
            };
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { SkipExisting = true };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(1);
            await Assert.That(result.FilesSkipped).IsEqualTo(1);
        }

        [Test]
        public async Task SyncProviderAsync_UpdatesExistingFiles_WhenSkipExistingIsFalse()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            var existingPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "existing.jpg", providerId: 1L);
            existingPhoto.ProviderFileId = "existing-file-id";
            existingPhoto.FileSize = 1000;
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("existing-file-id", "existing.jpg", MediaType.Photo, 5000)
            };
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { SkipExisting = false };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesUpdated).IsEqualTo(1);
            await Assert.That(result.FilesAdded).IsEqualTo(0);

            var updatedPhoto = await _db.Photos.FirstAsync(p => p.ProviderFileId == "existing-file-id");
            await Assert.That(updatedPhoto.FileSize).IsEqualTo(5000);
        }

        [Test]
        public async Task SyncProviderAsync_RemovesDeletedFiles_WhenRemoveDeletedIsTrue()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            var deletedPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "deleted.jpg", providerId: 1L);
            deletedPhoto.ProviderFileId = "deleted-file-id";
            _db.Photos.Add(deletedPhoto);
            await _db.SaveChangesAsync();

            // Provider returns empty file list — the existing photo is "deleted"
            var files = new List<StorageFileInfo>();
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { RemoveDeleted = true };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesRemoved).IsEqualTo(1);

            var remainingPhotos = await _db.Photos.Where(p => p.ProviderId == 1L).CountAsync();
            await Assert.That(remainingPhotos).IsEqualTo(0);
        }

        [Test]
        public async Task SyncProviderAsync_RespectsMaxFilesLimit()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1.jpg", "photo1.jpg", MediaType.Photo, 1000),
                TestHelpers.CreateTestStorageFileInfo("file2.jpg", "photo2.jpg", MediaType.Photo, 1000),
                TestHelpers.CreateTestStorageFileInfo("file3.jpg", "photo3.jpg", MediaType.Photo, 1000),
                TestHelpers.CreateTestStorageFileInfo("file4.jpg", "photo4.jpg", MediaType.Photo, 1000),
                TestHelpers.CreateTestStorageFileInfo("file5.jpg", "photo5.jpg", MediaType.Photo, 1000)
            };
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { MaxFiles = 2, RemoveDeleted = false };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(2);

            var photosInDb = await _db.Photos.Where(p => p.ProviderId == 1L).CountAsync();
            await Assert.That(photosInDb).IsEqualTo(2);
        }

        [Test]
        public async Task SyncProviderAsync_ReturnsFailedResult_WhenSyncAlreadyInProgress()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            // Use a TaskCompletionSource to hold the first sync in progress
            var tcs = new TaskCompletionSource<IEnumerable<StorageFileInfo>>();
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(tcs.Task);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Start first sync (will block on GetFilesAsync)
            var firstSync = _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Give the first sync time to register in _activeSyncs
            await Task.Delay(50);

            // Act — attempt second sync while first is in progress
            var secondResult = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(secondResult.Success).IsEqualTo(false);
            await Assert.That(secondResult.ErrorMessage).IsNotNull();
            await Assert.That(secondResult.ErrorMessage!).Contains("already in progress");

            // Clean up: complete the first sync
            tcs.SetResult(new List<StorageFileInfo>());
            await firstSync;
        }

        [Test]
        public async Task SyncProviderAsync_ReturnsFailedResult_WhenCancelled()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var cts = new CancellationTokenSource();
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var token = callInfo.Arg<CancellationToken>();
                    // Cancel while "scanning"
                    await cts.CancelAsync();
                    token.ThrowIfCancellationRequested();
                    return Enumerable.Empty<StorageFileInfo>();
                });
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, cts.Token);

            // Assert
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).IsNotNull();
            await Assert.That(result.ErrorMessage!).Contains("cancelled");
        }

        // --- GetSyncStatusAsync Tests ---

        [Test]
        public async Task GetSyncStatusAsync_ReturnsDefaultStatus_WhenNoSyncHasOccurred()
        {
            // Act
            var status = await _syncService.GetSyncStatusAsync(99L, CancellationToken.None);

            // Assert
            await Assert.That(status.ProviderId).IsEqualTo(99L);
            await Assert.That(status.IsInProgress).IsEqualTo(false);
            await Assert.That(status.LastSyncResult).IsNull();
        }

        [Test]
        public async Task GetSyncStatusAsync_ReturnsInProgressStatus_DuringSync()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var tcs = new TaskCompletionSource<IEnumerable<StorageFileInfo>>();
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(tcs.Task);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Start sync (will block on GetFilesAsync)
            var syncTask = _syncService.SyncProviderAsync(1L, request, CancellationToken.None);
            await Task.Delay(50);

            // Act
            var status = await _syncService.GetSyncStatusAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(status.IsInProgress).IsEqualTo(true);
            await Assert.That(status.ProviderId).IsEqualTo(1L);

            // Clean up
            tcs.SetResult(new List<StorageFileInfo>());
            await syncTask;
        }

        // --- CancelSync Tests ---

        [Test]
        public async Task CancelSync_ReturnsFalse_WhenNoActiveSync()
        {
            // Act
            var result = _syncService.CancelSync(99L);

            // Assert
            await Assert.That(result).IsEqualTo(false);
        }

        // --- ScanProviderAsync Tests ---

        [Test]
        public async Task ScanProviderAsync_ReturnsFileCountAndNewFilesInfo()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));

            // Pre-seed one existing photo
            var existingPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "existing.jpg", providerId: 1L);
            existingPhoto.ProviderFileId = "existing-file-id";
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("existing-file-id", "existing.jpg", MediaType.Photo, 2000),
                TestHelpers.CreateTestStorageFileInfo("new-file-1", "new1.jpg", MediaType.Photo, 3000),
                TestHelpers.CreateTestStorageFileInfo("new-file-2", "new2.jpg", MediaType.Photo, 4000)
            };
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Act
            var result = await _syncService.ScanProviderAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.ProviderId).IsEqualTo(1L);
            await Assert.That(result.TotalFilesFound).IsEqualTo(3);
            await Assert.That(result.NewFilesCount).IsEqualTo(2);
            await Assert.That(result.ExistingFilesCount).IsEqualTo(1);
            await Assert.That(result.NewFilesTotalSize).IsEqualTo(7000);
            await Assert.That(result.SampleNewFiles.Count).IsEqualTo(2);
        }

        [Test]
        public async Task ScanProviderAsync_ReturnsFailedResult_WhenProviderNotFound()
        {
            // Arrange
            _providerFactory.GetProviderAsync(99L, Arg.Any<CancellationToken>())
                .Returns((IStorageProvider?)null);

            // Act
            var result = await _syncService.ScanProviderAsync(99L, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).IsNotNull();
            await Assert.That(result.ErrorMessage!).Contains("not found");
        }

        // --- SyncAllProvidersAsync Tests ---

        [Test]
        public async Task SyncAllProvidersAsync_SyncsAllProviders()
        {
            // Arrange
            var provider1 = CreateMockProvider(1L, "Provider 1");
            var provider2 = CreateMockProvider(2L, "Provider 2");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Provider 1"));
            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(2L, "Provider 2"));
            await _db.SaveChangesAsync();

            var files1 = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("p1-file1", "photo1.jpg", MediaType.Photo, 1000)
            };
            var files2 = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("p2-file1", "photo2.jpg", MediaType.Photo, 2000),
                TestHelpers.CreateTestStorageFileInfo("p2-file2", "photo3.jpg", MediaType.Photo, 3000)
            };

            provider1.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(files1);
            provider2.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(files2);

            _providerFactory.GetAllProvidersAsync(Arg.Any<CancellationToken>())
                .Returns(new[] { provider1, provider2 });
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>()).Returns(provider1);
            _providerFactory.GetProviderAsync(2L, Arg.Any<CancellationToken>()).Returns(provider2);

            var request = new SyncRequest();

            // Act
            var results = (await _syncService.SyncAllProvidersAsync(request, CancellationToken.None)).ToList();

            // Assert
            await Assert.That(results.Count).IsEqualTo(2);
            await Assert.That(results[0].Success).IsEqualTo(true);
            await Assert.That(results[0].FilesAdded).IsEqualTo(1);
            await Assert.That(results[1].Success).IsEqualTo(true);
            await Assert.That(results[1].FilesAdded).IsEqualTo(2);
        }

        // --- Helpers ---

        private static IStorageProvider CreateMockProvider(long providerId, string displayName)
        {
            var provider = Substitute.For<IStorageProvider>();
            provider.ProviderId.Returns(providerId);
            provider.DisplayName.Returns(displayName);
            return provider;
        }
    }
}
