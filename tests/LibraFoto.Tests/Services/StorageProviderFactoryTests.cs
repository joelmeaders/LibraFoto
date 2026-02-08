using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.Configuration;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for StorageProviderFactory - core architectural component for provider management.
    /// Tests factory pattern, provider instantiation, configuration handling, caching, and error scenarios.
    /// Coverage: 21 branches including all provider types, error paths, and edge cases.
    /// </summary>
    public class StorageProviderFactoryTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private ServiceProvider _serviceProvider = null!;
        private StorageProviderFactory _factory = null!;
        private IConfiguration _configuration = null!;
        private string _testDirectory = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // Create unique in-memory database for each test
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .Options;

            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            // Setup test directory for local storage
            _testDirectory = TestHelpers.CreateTempDirectory();

            // Configure test settings
            var configData = new Dictionary<string, string?>
            {
                ["Storage:LocalPath"] = _testDirectory
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Setup DI container with all required services
            var services = new ServiceCollection();

            // Register DbContext factory for scoped resolution
            services.AddScoped<LibraFotoDbContext>(_ =>
            {
                var opts = new DbContextOptionsBuilder<LibraFotoDbContext>()
                    .UseSqlite(_connection)
                    .Options;
                return new LibraFotoDbContext(opts);
            });

            // Register storage dependencies
            services.AddScoped<IMediaScannerService, MediaScannerService>();
            services.AddHttpClient();
            services.AddSingleton<IConfiguration>(_configuration);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            _serviceProvider = services.BuildServiceProvider();

            // Create factory instance
            _factory = new StorageProviderFactory(
                _serviceProvider,
                _configuration,
                NullLogger<StorageProviderFactory>.Instance);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            _factory.ClearCache();
            _serviceProvider.Dispose();
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
            TestHelpers.CleanupTempDirectory(_testDirectory);
        }

        #region CreateProvider Tests

        [Test]
        public async Task CreateProvider_WithLocalType_ReturnsLocalStorageProvider()
        {
            // Act
            var provider = _factory.CreateProvider(StorageProviderType.Local);

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider).IsTypeOf<LocalStorageProvider>();
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.Local);
            await Assert.That(provider.SupportsUpload).IsTrue();
            await Assert.That(provider.SupportsWatch).IsTrue();
        }

        [Test]
        public async Task CreateProvider_WithGooglePhotosType_ReturnsGooglePhotosProvider()
        {
            // Act
            var provider = _factory.CreateProvider(StorageProviderType.GooglePhotos);

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider).IsTypeOf<GooglePhotosProvider>();
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task CreateProvider_WithGoogleDriveType_ThrowsNotImplementedException()
        {
            // Act & Assert
            await Assert.That(() => _factory.CreateProvider(StorageProviderType.GoogleDrive))
                .Throws<NotImplementedException>()
                .WithMessageContaining("Google Drive");
        }

        [Test]
        public async Task CreateProvider_WithOneDriveType_ThrowsNotImplementedException()
        {
            // Act & Assert
            await Assert.That(() => _factory.CreateProvider(StorageProviderType.OneDrive))
                .Throws<NotImplementedException>()
                .WithMessageContaining("OneDrive");
        }

        [Test]
        public async Task CreateProvider_WithInvalidType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var invalidType = (StorageProviderType)999;

            // Act & Assert
            await Assert.That(() => _factory.CreateProvider(invalidType))
                .Throws<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task CreateProvider_MultipleInstances_CreatesIndependentProviders()
        {
            // Act
            var provider1 = _factory.CreateProvider(StorageProviderType.Local);
            var provider2 = _factory.CreateProvider(StorageProviderType.Local);

            // Assert - Should be different instances
            await Assert.That(provider1).IsNotNull();
            await Assert.That(provider2).IsNotNull();
            await Assert.That(ReferenceEquals(provider1, provider2)).IsFalse();
        }

        #endregion

        #region GetProviderAsync Tests

        [Test]
        public async Task GetProviderAsync_WithExistingProvider_ReturnsInitializedProvider()
        {
            // Arrange
            var config = new LocalStorageConfiguration
            {
                BasePath = _testDirectory,
                OrganizeByDate = true,
                WatchForChanges = false
            };

            var entity = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "Test Local Storage",
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider!.ProviderId).IsEqualTo(1);
            await Assert.That(provider.DisplayName).IsEqualTo("Test Local Storage");
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.Local);
        }

        [Test]
        public async Task GetProviderAsync_WithDisabledProvider_ReturnsNull()
        {
            // Arrange
            var entity = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "Disabled Provider",
                IsEnabled = false,
                Configuration = null
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider).IsNull();
        }

        [Test]
        public async Task GetProviderAsync_WithNonExistentProvider_ReturnsNull()
        {
            // Act
            var provider = await _factory.GetProviderAsync(999);

            // Assert
            await Assert.That(provider).IsNull();
        }

        [Test]
        public async Task GetProviderAsync_CachesProvider_ReturnsSameInstanceOnSecondCall()
        {
            // Arrange
            var entity = TestHelpers.CreateTestStorageProvider(1, "Cached Provider");
            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider1 = await _factory.GetProviderAsync(1);
            var provider2 = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider1).IsNotNull();
            await Assert.That(provider2).IsNotNull();
            await Assert.That(ReferenceEquals(provider1, provider2)).IsTrue();
        }

        [Test]
        public async Task GetProviderAsync_WithNullConfiguration_InitializesProvider()
        {
            // Arrange
            var entity = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "No Config Provider",
                IsEnabled = true,
                Configuration = null
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider!.DisplayName).IsEqualTo("No Config Provider");
        }

        [Test]
        public async Task GetProviderAsync_WithEmptyConfiguration_InitializesProvider()
        {
            // Arrange
            var entity = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "Empty Config Provider",
                IsEnabled = true,
                Configuration = ""
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider).IsNotNull();
        }

        [Test]
        public async Task GetProviderAsync_WithMalformedJson_InitializesProviderWithDefaultConfig()
        {
            // Arrange
            var entity = new StorageProvider
            {
                Id = 1,
                Type = StorageProviderType.Local,
                Name = "Malformed Config Provider",
                IsEnabled = true,
                Configuration = "{invalid json"
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(1);

            // Assert - Should still create provider with default config
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider!.DisplayName).IsEqualTo("Malformed Config Provider");
        }

        [Test]
        public async Task GetProviderAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.That(async () =>
                await _factory.GetProviderAsync(1, cts.Token))
                .Throws<OperationCanceledException>();
        }

        #endregion

        #region GetAllProvidersAsync Tests

        [Test]
        public async Task GetAllProvidersAsync_WithNoProviders_ReturnsEmptyList()
        {
            // Act
            var providers = await _factory.GetAllProvidersAsync();

            // Assert
            await Assert.That(providers).IsEmpty();
        }

        [Test]
        public async Task GetAllProvidersAsync_WithMultipleProviders_ReturnsAllEnabled()
        {
            // Arrange
            var provider1 = TestHelpers.CreateTestStorageProvider(1, "Provider 1", StorageProviderType.Local, true);
            var provider2 = TestHelpers.CreateTestStorageProvider(2, "Provider 2", StorageProviderType.Local, true);
            var provider3 = TestHelpers.CreateTestStorageProvider(3, "Provider 3", StorageProviderType.GooglePhotos, true);
            var provider4 = TestHelpers.CreateTestStorageProvider(4, "Disabled Provider", StorageProviderType.Local, false);

            _db.StorageProviders.AddRange(provider1, provider2, provider3, provider4);
            await _db.SaveChangesAsync();

            // Act
            var providers = (await _factory.GetAllProvidersAsync()).ToList();

            // Assert
            await Assert.That(providers.Count).IsEqualTo(3);
            await Assert.That(providers.Any(p => p.ProviderId == 1)).IsTrue();
            await Assert.That(providers.Any(p => p.ProviderId == 2)).IsTrue();
            await Assert.That(providers.Any(p => p.ProviderId == 3)).IsTrue();
            await Assert.That(providers.Any(p => p.ProviderId == 4)).IsFalse();
        }

        [Test]
        public async Task GetAllProvidersAsync_UsesCachedProviders_WhenAvailable()
        {
            // Arrange
            var entity = TestHelpers.CreateTestStorageProvider(1, "Cached Provider");
            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Pre-populate cache
            var cachedProvider = await _factory.GetProviderAsync(1);

            // Act
            var providers = (await _factory.GetAllProvidersAsync()).ToList();

            // Assert
            await Assert.That(providers.Count).IsEqualTo(1);
            await Assert.That(ReferenceEquals(providers[0], cachedProvider)).IsTrue();
        }

        [Test]
        public async Task GetAllProvidersAsync_WithMixedTypes_ReturnsAllTypes()
        {
            // Arrange
            var local = TestHelpers.CreateTestStorageProvider(1, "Local", StorageProviderType.Local);
            var google = TestHelpers.CreateTestStorageProvider(2, "Google", StorageProviderType.GooglePhotos);

            _db.StorageProviders.AddRange(local, google);
            await _db.SaveChangesAsync();

            // Act
            var providers = (await _factory.GetAllProvidersAsync()).ToList();

            // Assert
            await Assert.That(providers.Count).IsEqualTo(2);
            await Assert.That(providers.Any(p => p.ProviderType == StorageProviderType.Local)).IsTrue();
            await Assert.That(providers.Any(p => p.ProviderType == StorageProviderType.GooglePhotos)).IsTrue();
        }

        #endregion

        #region GetProvidersByTypeAsync Tests

        [Test]
        public async Task GetProvidersByTypeAsync_WithMatchingType_ReturnsFilteredProviders()
        {
            // Arrange
            var local1 = TestHelpers.CreateTestStorageProvider(1, "Local 1", StorageProviderType.Local);
            var local2 = TestHelpers.CreateTestStorageProvider(2, "Local 2", StorageProviderType.Local);
            var google = TestHelpers.CreateTestStorageProvider(3, "Google", StorageProviderType.GooglePhotos);

            _db.StorageProviders.AddRange(local1, local2, google);
            await _db.SaveChangesAsync();

            // Act
            var providers = (await _factory.GetProvidersByTypeAsync(StorageProviderType.Local)).ToList();

            // Assert
            await Assert.That(providers.Count).IsEqualTo(2);
            await Assert.That(providers.All(p => p.ProviderType == StorageProviderType.Local)).IsTrue();
        }

        [Test]
        public async Task GetProvidersByTypeAsync_WithNoMatchingType_ReturnsEmptyList()
        {
            // Arrange
            var local = TestHelpers.CreateTestStorageProvider(1, "Local", StorageProviderType.Local);
            _db.StorageProviders.Add(local);
            await _db.SaveChangesAsync();

            // Act
            var providers = await _factory.GetProvidersByTypeAsync(StorageProviderType.GooglePhotos);

            // Assert
            await Assert.That(providers).IsEmpty();
        }

        [Test]
        public async Task GetProvidersByTypeAsync_OnlyReturnsEnabledProviders()
        {
            // Arrange
            var enabled = TestHelpers.CreateTestStorageProvider(1, "Enabled", StorageProviderType.Local, true);
            var disabled = TestHelpers.CreateTestStorageProvider(2, "Disabled", StorageProviderType.Local, false);

            _db.StorageProviders.AddRange(enabled, disabled);
            await _db.SaveChangesAsync();

            // Act
            var providers = (await _factory.GetProvidersByTypeAsync(StorageProviderType.Local)).ToList();

            // Assert
            await Assert.That(providers.Count).IsEqualTo(1);
            await Assert.That(providers[0].ProviderId).IsEqualTo(1);
        }

        #endregion

        #region GetOrCreateDefaultLocalProviderAsync Tests

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_WithExistingProvider_ReturnsExisting()
        {
            // Arrange
            var existing = TestHelpers.CreateTestStorageProvider(1, "Existing Local", StorageProviderType.Local);
            _db.StorageProviders.Add(existing);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider.ProviderId).IsEqualTo(1);
            await Assert.That(provider.DisplayName).IsEqualTo("Existing Local");

            // Verify no new provider was created
            var count = await _db.StorageProviders.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_WithNoProvider_CreatesNew()
        {
            // Act
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.Local);
            await Assert.That(provider.DisplayName).IsEqualTo("Local Storage");

            // Verify provider was persisted to database
            var dbProvider = await _db.StorageProviders.FirstOrDefaultAsync();
            await Assert.That(dbProvider).IsNotNull();
            await Assert.That(dbProvider!.Type).IsEqualTo(StorageProviderType.Local);
            await Assert.That(dbProvider.IsEnabled).IsTrue();
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_CreatesWithConfiguredPath()
        {
            // Act
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert
            var dbProvider = await _db.StorageProviders.FirstAsync();
            await Assert.That(dbProvider.Configuration).IsNotNull();
            await Assert.That(dbProvider.Configuration!).Contains(_testDirectory);
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_CreatesWithDefaultSettings()
        {
            // Act
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert
            var dbProvider = await _db.StorageProviders.FirstAsync();
            var config = JsonSerializer.Deserialize<LocalStorageConfiguration>(dbProvider.Configuration!);

            await Assert.That(config).IsNotNull();
            await Assert.That(config!.OrganizeByDate).IsTrue();
            await Assert.That(config.WatchForChanges).IsTrue();
            await Assert.That(config.BasePath).IsEqualTo(_testDirectory);
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_WithDisabledProvider_ReturnsIt()
        {
            // Arrange
            var disabled = TestHelpers.CreateTestStorageProvider(1, "Disabled Local", StorageProviderType.Local, false);
            _db.StorageProviders.Add(disabled);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert - Should return existing provider even if disabled
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider.ProviderId).IsEqualTo(1);

            // Verify no new provider was created
            var count = await _db.StorageProviders.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_UsesDefaultPathWhenNotConfigured()
        {
            // Arrange - Create factory without Storage:LocalPath configuration
            var emptyConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var factory = new StorageProviderFactory(
                _serviceProvider,
                emptyConfig,
                NullLogger<StorageProviderFactory>.Instance);

            // Act
            var provider = await factory.GetOrCreateDefaultLocalProviderAsync();

            // Assert
            await Assert.That(provider).IsNotNull();

            var dbProvider = await _db.StorageProviders.FirstAsync();
            var config = JsonSerializer.Deserialize<LocalStorageConfiguration>(dbProvider.Configuration!);

            await Assert.That(config).IsNotNull();
            // Should use LibraFotoDefaults.GetDefaultPhotosPath()
            await Assert.That(config!.BasePath).IsNotEmpty();
        }

        #endregion

        #region ClearCache Tests

        [Test]
        public async Task ClearCache_RemovesCachedProviders()
        {
            // Arrange
            var entity = TestHelpers.CreateTestStorageProvider(1, "Test Provider");
            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            var provider1 = await _factory.GetProviderAsync(1);

            // Act
            _factory.ClearCache();
            var provider2 = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider1).IsNotNull();
            await Assert.That(provider2).IsNotNull();
            await Assert.That(ReferenceEquals(provider1, provider2)).IsFalse();
        }

        [Test]
        public async Task ClearCache_AllowsNewProvidersToBeLoaded()
        {
            // Arrange
            var entity = TestHelpers.CreateTestStorageProvider(1, "Original Name");
            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            var provider1 = await _factory.GetProviderAsync(1);

            // Update provider name
            entity.Name = "Updated Name";
            await _db.SaveChangesAsync();

            // Act
            _factory.ClearCache();
            var provider2 = await _factory.GetProviderAsync(1);

            // Assert
            await Assert.That(provider1!.DisplayName).IsEqualTo("Original Name");
            await Assert.That(provider2!.DisplayName).IsEqualTo("Updated Name");
        }

        [Test]
        public async Task ClearCache_IsThreadSafe()
        {
            // Arrange
            var entity = TestHelpers.CreateTestStorageProvider(1, "Test Provider");
            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            await _factory.GetProviderAsync(1);

            // Act - Multiple threads clearing cache simultaneously
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => _factory.ClearCache()))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert - Should complete without exceptions
            var provider = await _factory.GetProviderAsync(1);
            await Assert.That(provider).IsNotNull();
        }

        #endregion

        #region Provider Initialization Tests

        [Test]
        public async Task GetProviderAsync_InitializesProviderWithCorrectValues()
        {
            // Arrange
            var config = new LocalStorageConfiguration
            {
                BasePath = "/custom/path",
                OrganizeByDate = false,
                WatchForChanges = true,
                MaxImportDimension = 3840
            };

            var entity = new StorageProvider
            {
                Id = 42,
                Type = StorageProviderType.Local,
                Name = "Custom Storage",
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };

            _db.StorageProviders.Add(entity);
            await _db.SaveChangesAsync();

            // Act
            var provider = await _factory.GetProviderAsync(42);

            // Assert
            await Assert.That(provider).IsNotNull();
            await Assert.That(provider!.ProviderId).IsEqualTo(42);
            await Assert.That(provider.DisplayName).IsEqualTo("Custom Storage");
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.Local);
        }

        [Test]
        public async Task GetProviderAsync_InitializesMultipleProviderTypes()
        {
            // Arrange
            var local = TestHelpers.CreateTestStorageProvider(1, "Local", StorageProviderType.Local);
            var google = new StorageProvider
            {
                Id = 2,
                Type = StorageProviderType.GooglePhotos,
                Name = "Google Photos",
                IsEnabled = true,
                Configuration = null
            };

            _db.StorageProviders.AddRange(local, google);
            await _db.SaveChangesAsync();

            // Act
            var localProvider = await _factory.GetProviderAsync(1);
            var googleProvider = await _factory.GetProviderAsync(2);

            // Assert
            await Assert.That(localProvider).IsNotNull();
            await Assert.That(localProvider).IsTypeOf<LocalStorageProvider>();
            await Assert.That(googleProvider).IsNotNull();
            await Assert.That(googleProvider).IsTypeOf<GooglePhotosProvider>();
        }

        #endregion

        #region Edge Cases and Error Handling

        [Test]
        public async Task GetProviderAsync_WithZeroId_ReturnsNull()
        {
            // Act
            var provider = await _factory.GetProviderAsync(0);

            // Assert
            await Assert.That(provider).IsNull();
        }

        [Test]
        public async Task GetProviderAsync_WithNegativeId_ReturnsNull()
        {
            // Act
            var provider = await _factory.GetProviderAsync(-1);

            // Assert
            await Assert.That(provider).IsNull();
        }

        [Test]
        public async Task GetAllProvidersAsync_WithLargeNumberOfProviders_HandlesCorrectly()
        {
            // Arrange - Create 50 providers
            var providers = Enumerable.Range(1, 50)
                .Select(i => TestHelpers.CreateTestStorageProvider(i, $"Provider {i}"))
                .ToList();

            _db.StorageProviders.AddRange(providers);
            await _db.SaveChangesAsync();

            // Act
            var result = (await _factory.GetAllProvidersAsync()).ToList();

            // Assert
            await Assert.That(result.Count).IsEqualTo(50);
            await Assert.That(result.All(p => p.ProviderId >= 1 && p.ProviderId <= 50)).IsTrue();
        }

        [Test]
        public async Task CreateProvider_WithAllSupportedTypes_CreatesCorrectInstances()
        {
            // Act & Assert
            var local = _factory.CreateProvider(StorageProviderType.Local);
            await Assert.That(local.ProviderType).IsEqualTo(StorageProviderType.Local);

            var google = _factory.CreateProvider(StorageProviderType.GooglePhotos);
            await Assert.That(google.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        #endregion
    }
}
