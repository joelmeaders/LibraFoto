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

        [Test]
        public async Task SyncAllProvidersAsync_StopsOnCancellation()
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

            provider1.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(files1);

            _providerFactory.GetAllProvidersAsync(Arg.Any<CancellationToken>())
                .Returns(new[] { provider1, provider2 });
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>()).Returns(provider1);
            _providerFactory.GetProviderAsync(2L, Arg.Any<CancellationToken>()).Returns(provider2);

            var cts = new CancellationTokenSource();
            var request = new SyncRequest();

            // Cancel after first provider
            cts.Cancel();

            // Act & Assert - should throw OperationCanceledException
            await Assert.That(async () =>
                await _syncService.SyncAllProvidersAsync(request, cts.Token))
                .Throws<OperationCanceledException>();
        }

        [Test]
        public async Task SyncAllProvidersAsync_ReturnsEmpty_WhenNoProviders()
        {
            // Arrange
            _providerFactory.GetAllProvidersAsync(Arg.Any<CancellationToken>())
                .Returns(Enumerable.Empty<IStorageProvider>());

            var request = new SyncRequest();

            // Act
            var results = (await _syncService.SyncAllProvidersAsync(request, CancellationToken.None)).ToList();

            // Assert
            await Assert.That(results).IsEmpty();
        }

        // --- Extended Sync Scenario Tests ---

        [Test]
        public async Task SyncProviderAsync_FiltersOutFolders()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                new() { FileId = "folder1", FileName = "Folder", IsFolder = true },
                TestHelpers.CreateTestStorageFileInfo("file1", "photo1.jpg", MediaType.Photo, 1000),
                new() { FileId = "folder2", FileName = "Another Folder", IsFolder = true }
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(1);
            await Assert.That(result.TotalFilesFound).IsEqualTo(1);

            var photosInDb = await _db.Photos.CountAsync();
            await Assert.That(photosInDb).IsEqualTo(1);
        }

        [Test]
        public async Task SyncProviderAsync_HandlesEmptyFileList()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Enumerable.Empty<StorageFileInfo>());
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(0);
            await Assert.That(result.TotalFilesFound).IsEqualTo(0);
        }

        [Test]
        public async Task SyncProviderAsync_UpdatesLastSyncDate()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            var storageProvider = TestHelpers.CreateTestStorageProvider(1L, "Test Provider");
            storageProvider.LastSyncDate = null;
            _db.StorageProviders.Add(storageProvider);
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1", "photo1.jpg", MediaType.Photo, 1000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var beforeSync = DateTime.UtcNow;
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);
            var afterSync = DateTime.UtcNow;

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);

            var updatedProvider = await _db.StorageProviders.FindAsync(1L);
            await Assert.That(updatedProvider).IsNotNull();
            await Assert.That(updatedProvider!.LastSyncDate).IsNotNull();
            await Assert.That(updatedProvider.LastSyncDate!.Value).IsGreaterThanOrEqualTo(beforeSync);
            await Assert.That(updatedProvider.LastSyncDate!.Value).IsLessThanOrEqualTo(afterSync);
        }

        [Test]
        public async Task SyncProviderAsync_HandlesPartialFailures_WithErrors()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            // Create files where one will cause an error (null FileName simulation)
            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1", "good1.jpg", MediaType.Photo, 1000),
                new StorageFileInfo { FileId = "bad-file", FileName = null!, FileSize = 2000, IsFolder = false },
                TestHelpers.CreateTestStorageFileInfo("file2", "good2.jpg", MediaType.Photo, 3000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.Errors.Count).IsGreaterThan(0);

            // Should still process the good files
            var photosInDb = await _db.Photos.CountAsync();
            await Assert.That(photosInDb).IsEqualTo(2);
        }

        [Test]
        public async Task SyncProviderAsync_BatchSavesEvery10Files()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            // Create 25 files to test batching
            var files = Enumerable.Range(1, 25).Select(i =>
                TestHelpers.CreateTestStorageFileInfo($"file{i}", $"photo{i}.jpg", MediaType.Photo, 1000)
            ).ToList();

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(25);

            var photosInDb = await _db.Photos.CountAsync();
            await Assert.That(photosInDb).IsEqualTo(25);
        }

        [Test]
        public async Task SyncProviderAsync_HandlesProviderException()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns<IEnumerable<StorageFileInfo>>(_ => throw new InvalidOperationException("Provider error"));
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).IsNotNull();
            await Assert.That(result.ErrorMessage!).Contains("Provider error");
        }

        [Test]
        public async Task SyncProviderAsync_MixedNewUpdatedDeletedFiles()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));

            // Pre-seed existing and to-be-deleted photos
            var existingPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "existing.jpg", providerId: 1L);
            existingPhoto.ProviderFileId = "existing-id";
            existingPhoto.FileSize = 1000;

            var deletedPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "deleted.jpg", providerId: 1L);
            deletedPhoto.ProviderFileId = "deleted-id";

            _db.Photos.AddRange(existingPhoto, deletedPhoto);
            await _db.SaveChangesAsync();

            // Provider returns: 1 existing (to update), 1 new
            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("existing-id", "existing.jpg", MediaType.Photo, 5000),
                TestHelpers.CreateTestStorageFileInfo("new-id", "new.jpg", MediaType.Photo, 3000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { SkipExisting = false, RemoveDeleted = true };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(1);
            await Assert.That(result.FilesUpdated).IsEqualTo(1);
            await Assert.That(result.FilesRemoved).IsEqualTo(1);

            var remainingPhotos = await _db.Photos.Where(p => p.ProviderId == 1L).ToListAsync();
            await Assert.That(remainingPhotos.Count).IsEqualTo(2);

            var updatedPhoto = remainingPhotos.First(p => p.ProviderFileId == "existing-id");
            await Assert.That(updatedPhoto.FileSize).IsEqualTo(5000);
        }

        [Test]
        public async Task SyncProviderAsync_DoesNotRemoveDeleted_WhenRemoveDeletedIsFalse()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));

            var deletedPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "deleted.jpg", providerId: 1L);
            deletedPhoto.ProviderFileId = "deleted-id";
            _db.Photos.Add(deletedPhoto);
            await _db.SaveChangesAsync();

            // Provider returns empty list
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Enumerable.Empty<StorageFileInfo>());
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { RemoveDeleted = false };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesRemoved).IsEqualTo(0);

            var remainingPhotos = await _db.Photos.CountAsync();
            await Assert.That(remainingPhotos).IsEqualTo(1);
        }

        [Test]
        public async Task SyncProviderAsync_SetsMediaTypeForVideos()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("video1", "movie.mp4", MediaType.Video, 50000)
            };

            files[0] = files[0] with { Duration = 120 }; // 2 minute video

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(1);

            var video = await _db.Photos.FirstAsync();
            await Assert.That(video.MediaType).IsEqualTo(MediaType.Video);
            await Assert.That(video.Duration).IsEqualTo(120);
        }

        [Test]
        public async Task SyncProviderAsync_PreservesDateTaken()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var createdDate = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            var fileInfo = TestHelpers.CreateTestStorageFileInfo("file1", "photo.jpg", MediaType.Photo, 1000);
            fileInfo = fileInfo with { CreatedDate = createdDate };

            var files = new List<StorageFileInfo> { fileInfo };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);

            var photo = await _db.Photos.FirstAsync();
            await Assert.That(photo.DateTaken).IsEqualTo(createdDate);
        }

        [Test]
        public async Task SyncProviderAsync_SetsOriginalFilename()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1", "IMG_1234.jpg", MediaType.Photo, 1000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            var photo = await _db.Photos.FirstAsync();
            await Assert.That(photo.Filename).IsEqualTo("IMG_1234.jpg");
            await Assert.That(photo.OriginalFilename).IsEqualTo("IMG_1234.jpg");
        }

        [Test]
        public async Task SyncProviderAsync_UpdatesWidthAndHeight()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));

            var existingPhoto = TestHelpers.CreateTestPhoto(id: 0, filename: "photo.jpg", providerId: 1L);
            existingPhoto.ProviderFileId = "photo-id";
            existingPhoto.Width = 100;
            existingPhoto.Height = 100;
            _db.Photos.Add(existingPhoto);
            await _db.SaveChangesAsync();

            var fileInfo = TestHelpers.CreateTestStorageFileInfo("photo-id", "photo.jpg", MediaType.Photo, 5000);
            fileInfo = fileInfo with { Width = 1920, Height = 1080 };
            var files = new List<StorageFileInfo> { fileInfo };

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

            var updatedPhoto = await _db.Photos.FirstAsync();
            await Assert.That(updatedPhoto.Width).IsEqualTo(1920);
            await Assert.That(updatedPhoto.Height).IsEqualTo(1080);
        }

        [Test]
        public async Task GetSyncStatusAsync_ReturnsLatestSyncResult()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1", "photo1.jpg", MediaType.Photo, 1000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Perform a sync
            var request = new SyncRequest();
            await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Act
            var status = await _syncService.GetSyncStatusAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(status.ProviderId).IsEqualTo(1L);
            await Assert.That(status.IsInProgress).IsEqualTo(false);
            await Assert.That(status.LastSyncResult).IsNotNull();
            await Assert.That(status.LastSyncResult!.Success).IsEqualTo(true);
            await Assert.That(status.LastSyncResult.FilesAdded).IsEqualTo(1);
        }

        [Test]
        public async Task CancelSync_StopsActiveSync()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var tcs = new TaskCompletionSource<IEnumerable<StorageFileInfo>>();
            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var token = callInfo.Arg<CancellationToken>();
                    return tcs.Task.ContinueWith(t =>
                    {
                        token.ThrowIfCancellationRequested();
                        return t.Result;
                    }, token);
                });
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Start sync
            var syncTask = _syncService.SyncProviderAsync(1L, request, CancellationToken.None);
            await Task.Delay(50);

            // Act
            var cancelled = _syncService.CancelSync(1L);

            // Assert
            await Assert.That(cancelled).IsEqualTo(true);

            // Complete the provider call
            tcs.SetResult(new List<StorageFileInfo>());

            var result = await syncTask;
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).Contains("cancelled");
        }

        [Test]
        public async Task ScanProviderAsync_HandlesLargeFileCounts()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            // Create 100 files, 50 existing, 50 new
            var existingFiles = Enumerable.Range(1, 50).Select(i =>
            {
                var photo = TestHelpers.CreateTestPhoto(id: 0, filename: $"existing{i}.jpg", providerId: 1L);
                photo.ProviderFileId = $"existing-{i}";
                return photo;
            });
            _db.Photos.AddRange(existingFiles);
            await _db.SaveChangesAsync();

            var allFiles = Enumerable.Range(1, 50)
                .Select(i => TestHelpers.CreateTestStorageFileInfo($"existing-{i}", $"existing{i}.jpg", MediaType.Photo, 1000))
                .Concat(Enumerable.Range(1, 50)
                    .Select(i => TestHelpers.CreateTestStorageFileInfo($"new-{i}", $"new{i}.jpg", MediaType.Photo, 2000)))
                .ToList();

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(allFiles);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Act
            var result = await _syncService.ScanProviderAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.TotalFilesFound).IsEqualTo(100);
            await Assert.That(result.NewFilesCount).IsEqualTo(50);
            await Assert.That(result.ExistingFilesCount).IsEqualTo(50);
            await Assert.That(result.NewFilesTotalSize).IsEqualTo(100000); // 50 * 2000
            await Assert.That(result.SampleNewFiles.Count).IsEqualTo(10); // Sample limited to 10
        }

        [Test]
        public async Task ScanProviderAsync_HandlesProviderException()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns<IEnumerable<StorageFileInfo>>(_ => throw new UnauthorizedAccessException("Access denied"));
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            // Act
            var result = await _syncService.ScanProviderAsync(1L, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(false);
            await Assert.That(result.ErrorMessage).IsNotNull();
            await Assert.That(result.ErrorMessage!).Contains("Access denied");
        }

        [Test]
        public async Task SyncProviderAsync_SpecificFolderId_PassedToProvider()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var files = new List<StorageFileInfo>
            {
                TestHelpers.CreateTestStorageFileInfo("file1", "photo1.jpg", MediaType.Photo, 1000)
            };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest { FolderId = "specific-folder-123" };

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);

            // Verify GetFilesAsync was called with the specific folder ID
            await provider.Received(1).GetFilesAsync("specific-folder-123", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SyncProviderAsync_HandlesNullFullPath()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var fileInfo = TestHelpers.CreateTestStorageFileInfo("file-id-1", "photo.jpg", MediaType.Photo, 1000);
            fileInfo = fileInfo with { FullPath = null }; // Simulate no FullPath
            var files = new List<StorageFileInfo> { fileInfo };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);
            await Assert.That(result.FilesAdded).IsEqualTo(1);

            var photo = await _db.Photos.FirstAsync();
            // When FullPath is null, FileId is used as FilePath
            await Assert.That(photo.FilePath).IsEqualTo("file-id-1");
        }

        [Test]
        public async Task SyncProviderAsync_HandlesNullWidthHeight()
        {
            // Arrange
            var provider = CreateMockProvider(1L, "Test Provider");

            _db.StorageProviders.Add(TestHelpers.CreateTestStorageProvider(1L, "Test Provider"));
            await _db.SaveChangesAsync();

            var fileInfo = TestHelpers.CreateTestStorageFileInfo("file1", "photo.jpg", MediaType.Photo, 1000);
            fileInfo = fileInfo with { Width = null, Height = null };
            var files = new List<StorageFileInfo> { fileInfo };

            provider.GetFilesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(files);
            _providerFactory.GetProviderAsync(1L, Arg.Any<CancellationToken>())
                .Returns(provider);

            var request = new SyncRequest();

            // Act
            var result = await _syncService.SyncProviderAsync(1L, request, CancellationToken.None);

            // Assert
            await Assert.That(result.Success).IsEqualTo(true);

            var photo = await _db.Photos.FirstAsync();
            await Assert.That(photo.Width).IsEqualTo(0);
            await Assert.That(photo.Height).IsEqualTo(0);
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
