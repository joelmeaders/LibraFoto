using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using LibraFoto.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Services
{
    /// <summary>
    /// Factory for creating and managing storage provider instances.
    /// </summary>
    public class StorageProviderFactory : IStorageProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StorageProviderFactory> _logger;
        private readonly Dictionary<long, IStorageProvider> _providerCache = [];
        private readonly object _cacheLock = new();

        public StorageProviderFactory(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<StorageProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IStorageProvider?> GetProviderAsync(long providerId, CancellationToken cancellationToken = default)
        {
            lock (_cacheLock)
            {
                if (_providerCache.TryGetValue(providerId, out var cached))
                {
                    return cached;
                }
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            var entity = await dbContext.StorageProviders
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == providerId && p.IsEnabled, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            var provider = CreateProvider(entity.Type);
            provider.Initialize(entity.Id, entity.Name, entity.Configuration);

            lock (_cacheLock)
            {
                _providerCache[providerId] = provider;
            }

            return provider;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IStorageProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            var entities = await dbContext.StorageProviders
                .AsNoTracking()
                .Where(p => p.IsEnabled)
                .ToListAsync(cancellationToken);

            var providers = new List<IStorageProvider>();

            foreach (var entity in entities)
            {
                lock (_cacheLock)
                {
                    if (_providerCache.TryGetValue(entity.Id, out var cached))
                    {
                        providers.Add(cached);
                        continue;
                    }
                }

                var provider = CreateProvider(entity.Type);
                provider.Initialize(entity.Id, entity.Name, entity.Configuration);

                lock (_cacheLock)
                {
                    _providerCache[entity.Id] = provider;
                }

                providers.Add(provider);
            }

            return providers;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<IStorageProvider>> GetProvidersByTypeAsync(
            StorageProviderType type,
            CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            var entities = await dbContext.StorageProviders
                .AsNoTracking()
                .Where(p => p.Type == type && p.IsEnabled)
                .ToListAsync(cancellationToken);

            var providers = new List<IStorageProvider>();

            foreach (var entity in entities)
            {
                lock (_cacheLock)
                {
                    if (_providerCache.TryGetValue(entity.Id, out var cached))
                    {
                        providers.Add(cached);
                        continue;
                    }
                }

                var provider = CreateProvider(entity.Type);
                provider.Initialize(entity.Id, entity.Name, entity.Configuration);

                lock (_cacheLock)
                {
                    _providerCache[entity.Id] = provider;
                }

                providers.Add(provider);
            }

            return providers;
        }

        /// <inheritdoc />
        public IStorageProvider CreateProvider(StorageProviderType type)
        {
            return type switch
            {
                StorageProviderType.Local => new LocalStorageProvider(
                    _serviceProvider.GetRequiredService<IMediaScannerService>(),
                    _configuration,
                    _serviceProvider.GetRequiredService<ILogger<LocalStorageProvider>>()),
                StorageProviderType.GooglePhotos => new GooglePhotosProvider(
                    _serviceProvider.GetRequiredService<ILogger<GooglePhotosProvider>>(),
                    _serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    _serviceProvider.GetRequiredService<LibraFotoDbContext>()),
                StorageProviderType.GoogleDrive => throw new NotImplementedException("Google Drive provider not yet implemented"),
                StorageProviderType.OneDrive => throw new NotImplementedException("OneDrive provider not yet implemented"),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown storage provider type: {type}")
            };
        }

        /// <inheritdoc />
        public async Task<IStorageProvider> GetOrCreateDefaultLocalProviderAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            // Try to find existing local provider
            var localProvider = await dbContext.StorageProviders
                .FirstOrDefaultAsync(p => p.Type == StorageProviderType.Local, cancellationToken);

            if (localProvider != null)
            {
                var provider = CreateProvider(StorageProviderType.Local);
                provider.Initialize(localProvider.Id, localProvider.Name, localProvider.Configuration);
                return provider;
            }

            // Create default local provider
            var defaultPath = _configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
            var config = new LocalStorageConfiguration
            {
                BasePath = defaultPath,
                OrganizeByDate = true,
                WatchForChanges = true
            };

            var newProvider = new Data.Entities.StorageProvider
            {
                Type = StorageProviderType.Local,
                Name = "Local Storage",
                IsEnabled = true,
                Configuration = JsonSerializer.Serialize(config)
            };

            dbContext.StorageProviders.Add(newProvider);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created default local storage provider at {Path}", defaultPath);

            var storageProvider = CreateProvider(StorageProviderType.Local);
            storageProvider.Initialize(newProvider.Id, newProvider.Name, newProvider.Configuration);
            return storageProvider;
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _providerCache.Clear();
            }
        }
    }
}
