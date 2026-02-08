using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Tests for StorageProviderFactory.
    /// </summary>
    public class StorageProviderFactoryTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IServiceProvider _serviceProvider = null!;
        private IConfiguration _configuration = null!;
        private StorageProviderFactory _factory = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // SQLite in-memory with shared cache (unique per test)
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .Options;

            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            // Mock scoped service provider for DbContext resolution
            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.GetService(typeof(LibraFotoDbContext)).Returns(_db);

            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            scopeFactory.CreateScope().Returns(scope);

            _serviceProvider = Substitute.For<IServiceProvider>();
            _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

            // Services needed by CreateProvider for Local type
            _serviceProvider.GetService(typeof(IMediaScannerService))
                .Returns(Substitute.For<IMediaScannerService>());
            _serviceProvider.GetService(typeof(ILogger<LocalStorageProvider>))
                .Returns(NullLogger<LocalStorageProvider>.Instance);

            // Services needed by CreateProvider for GooglePhotos type
            _serviceProvider.GetService(typeof(IHttpClientFactory))
                .Returns(Substitute.For<IHttpClientFactory>());
            _serviceProvider.GetService(typeof(ILogger<GooglePhotosProvider>))
                .Returns(NullLogger<GooglePhotosProvider>.Instance);
            _serviceProvider.GetService(typeof(LibraFotoDbContext))
                .Returns(_db);

            _configuration = Substitute.For<IConfiguration>();
            _configuration["Storage:LocalPath"].Returns("/test/photos");

            var logger = NullLogger<StorageProviderFactory>.Instance;
            _factory = new StorageProviderFactory(_serviceProvider, _configuration, logger);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        #region GetProviderAsync Tests

        [Test]
        public async Task GetProviderAsync_ReturnsNull_WhenProviderNotInDatabase()
        {
            var result = await _factory.GetProviderAsync(999);

            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetProviderAsync_ReturnsNull_WhenProviderIsDisabled()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Disabled Provider",
                Type = StorageProviderType.Local,
                IsEnabled = false,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();
            var id = (await _db.StorageProviders.FirstAsync()).Id;

            var result = await _factory.GetProviderAsync(id);

            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetProviderAsync_ReturnsProvider_WhenFoundAndEnabled()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();
            var id = (await _db.StorageProviders.FirstAsync()).Id;

            var result = await _factory.GetProviderAsync(id);

            await Assert.That(result).IsNotNull();
            await Assert.That(result is LocalStorageProvider).IsTrue();
        }

        [Test]
        public async Task GetProviderAsync_CachesProvider_ReturnsSameInstanceOnSecondCall()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();
            var id = (await _db.StorageProviders.FirstAsync()).Id;

            var first = await _factory.GetProviderAsync(id);
            var second = await _factory.GetProviderAsync(id);

            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsNotNull();
            await Assert.That(object.ReferenceEquals(first, second)).IsTrue();
        }

        #endregion

        #region GetAllProvidersAsync Tests

        [Test]
        public async Task GetAllProvidersAsync_ReturnsOnlyEnabledProviders()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Enabled Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Disabled Provider",
                Type = StorageProviderType.Local,
                IsEnabled = false,
                Configuration = """{"BasePath":"./photos2","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();

            var result = (await _factory.GetAllProvidersAsync()).ToList();

            await Assert.That(result).Count().IsEqualTo(1);
        }

        [Test]
        public async Task GetAllProvidersAsync_ReturnsEmpty_WhenNoProviders()
        {
            var result = (await _factory.GetAllProvidersAsync()).ToList();

            await Assert.That(result).IsEmpty();
        }

        #endregion

        #region GetProvidersByTypeAsync Tests

        [Test]
        public async Task GetProvidersByTypeAsync_ReturnsProvidersOfMatchingType()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local 1",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local 2",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos2","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Google Photos",
                Type = StorageProviderType.GooglePhotos,
                IsEnabled = true
            });
            await _db.SaveChangesAsync();

            var result = (await _factory.GetProvidersByTypeAsync(StorageProviderType.Local)).ToList();

            await Assert.That(result).Count().IsEqualTo(2);
        }

        [Test]
        public async Task GetProvidersByTypeAsync_ReturnsEmpty_WhenNoMatchingType()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();

            var result = (await _factory.GetProvidersByTypeAsync(StorageProviderType.GooglePhotos)).ToList();

            await Assert.That(result).IsEmpty();
        }

        #endregion

        #region CreateProvider Tests

        [Test]
        public async Task CreateProvider_LocalType_CreatesLocalStorageProvider()
        {
            var provider = _factory.CreateProvider(StorageProviderType.Local);

            await Assert.That(provider).IsNotNull();
            await Assert.That(provider is LocalStorageProvider).IsTrue();
        }

        [Test]
        public async Task CreateProvider_GoogleDrive_ThrowsNotImplementedException()
        {
            await Assert.That(() => _factory.CreateProvider(StorageProviderType.GoogleDrive))
                .Throws<NotImplementedException>();
        }

        [Test]
        public async Task CreateProvider_OneDrive_ThrowsNotImplementedException()
        {
            await Assert.That(() => _factory.CreateProvider(StorageProviderType.OneDrive))
                .Throws<NotImplementedException>();
        }

        [Test]
        public async Task CreateProvider_InvalidType_ThrowsArgumentOutOfRangeException()
        {
            await Assert.That(() => _factory.CreateProvider((StorageProviderType)99))
                .Throws<ArgumentOutOfRangeException>();
        }

        #endregion

        #region ClearCache Tests

        [Test]
        public async Task ClearCache_ClearsInternalProviderCache()
        {
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Local Provider",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./photos","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();
            var id = (await _db.StorageProviders.FirstAsync()).Id;

            // First call populates cache
            var first = await _factory.GetProviderAsync(id);

            // Clear cache
            _factory.ClearCache();

            // Second call should create a new instance (not from cache)
            var second = await _factory.GetProviderAsync(id);

            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsNotNull();
            await Assert.That(object.ReferenceEquals(first, second)).IsFalse();
        }

        #endregion

        #region GetOrCreateDefaultLocalProviderAsync Tests

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_CreatesLocalProvider_WhenNoneExists()
        {
            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            await Assert.That(provider).IsNotNull();
            await Assert.That(provider is LocalStorageProvider).IsTrue();

            // Verify provider was persisted in the database
            var dbCount = await _db.StorageProviders.CountAsync();
            await Assert.That(dbCount).IsEqualTo(1);

            var dbProvider = await _db.StorageProviders.FirstAsync();
            await Assert.That(dbProvider.Type).IsEqualTo(StorageProviderType.Local);
            await Assert.That(dbProvider.Name).IsEqualTo("Local Storage");
            await Assert.That(dbProvider.IsEnabled).IsTrue();
        }

        [Test]
        public async Task GetOrCreateDefaultLocalProviderAsync_ReturnsExistingLocalProvider()
        {
            // Seed an existing local provider
            _db.StorageProviders.Add(new StorageProvider
            {
                Name = "Existing Local",
                Type = StorageProviderType.Local,
                IsEnabled = true,
                Configuration = """{"BasePath":"./existing","OrganizeByDate":true,"WatchForChanges":false}"""
            });
            await _db.SaveChangesAsync();

            var provider = await _factory.GetOrCreateDefaultLocalProviderAsync();

            await Assert.That(provider).IsNotNull();
            await Assert.That(provider is LocalStorageProvider).IsTrue();

            // Verify no additional provider was created
            var dbCount = await _db.StorageProviders.CountAsync();
            await Assert.That(dbCount).IsEqualTo(1);
        }

        #endregion
    }
}
