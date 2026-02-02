using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Storage;

/// <summary>
/// Tests for storage provider deletion functionality with OAuth disconnect.
/// </summary>
public class StorageProviderDeletionTests
{
    private static async Task<(GooglePhotosProvider provider, LibraFotoDbContext dbContext)> CreateProviderAsync()
    {
        var logger = NullLogger<GooglePhotosProvider>.Instance;
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());
        var cacheService = Substitute.For<ICacheService>();

        var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        var dbContext = new LibraFotoDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var provider = new GooglePhotosProvider(logger, httpClientFactory, cacheService, dbContext);
        return (provider, dbContext);
    }

    [Test]
    public async Task DisconnectAsync_ClearsOAuthTokensAndDisablesProvider()
    {
        // Arrange
        var (provider, dbContext) = await CreateProviderAsync();
        await using var _ = dbContext;

        var storageProvider = new StorageProvider
        {
            Id = 1,
            Type = StorageProviderType.GooglePhotos,
            Name = "TestProvider",
            IsEnabled = true,
            Configuration = JsonSerializer.Serialize(new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-secret",
                RefreshToken = "test-refresh-token",
                AccessToken = "test-access-token",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1)
            })
        };

        // Act
        var result = await provider.DisconnectAsync(storageProvider, CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(storageProvider.IsEnabled).IsFalse();

        // Verify tokens are cleared
        var config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(storageProvider.Configuration!);
        await Assert.That(config).IsNotNull();
        await Assert.That(config!.RefreshToken).IsNull();
        await Assert.That(config.AccessToken).IsNull();
        await Assert.That(config.AccessTokenExpiry).IsNull();
        await Assert.That(config.GrantedScopes).IsNull();
    }

    [Test]
    public async Task DisconnectAsync_WithInvalidConfiguration_ReturnsFalse()
    {
        // Arrange
        var (provider, dbContext) = await CreateProviderAsync();
        await using var _ = dbContext;

        var storageProvider = new StorageProvider
        {
            Id = 1,
            Type = StorageProviderType.GooglePhotos,
            Name = "TestProvider",
            IsEnabled = true,
            Configuration = "invalid-json"
        };

        // Act
        var result = await provider.DisconnectAsync(storageProvider, CancellationToken.None);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(storageProvider.IsEnabled).IsFalse(); // Still disables even on error
    }

    [Test]
    public async Task DisconnectAsync_WithEmptyConfiguration_ReturnsTrue()
    {
        // Arrange
        var (provider, dbContext) = await CreateProviderAsync();
        await using var _ = dbContext;

        var storageProvider = new StorageProvider
        {
            Id = 1,
            Type = StorageProviderType.GooglePhotos,
            Name = "TestProvider",
            IsEnabled = true,
            Configuration = null
        };

        // Act
        var result = await provider.DisconnectAsync(storageProvider, CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(storageProvider.IsEnabled).IsFalse();
    }

    [Test]
    public async Task DisconnectAsync_PreservesNonTokenConfigurationFields()
    {
        // Arrange
        var (provider, dbContext) = await CreateProviderAsync();
        await using var _ = dbContext;

        var originalClientId = "my-client-id";
        var originalConfiguration = new GooglePhotosConfiguration
        {
            ClientId = originalClientId,
            ClientSecret = "my-secret",
            RefreshToken = "refresh-token-to-clear",
            AccessToken = "access-token-to-clear",
            AccessTokenExpiry = DateTime.UtcNow.AddHours(1),
            GrantedScopes = ["scope1", "scope2"],
            EnableLocalCache = true,
            MaxCacheSizeBytes = 1024 * 1024 * 1024
        };

        var storageProvider = new StorageProvider
        {
            Id = 1,
            Type = StorageProviderType.GooglePhotos,
            Name = "TestProvider",
            IsEnabled = true,
            Configuration = JsonSerializer.Serialize(originalConfiguration)
        };

        // Act
        var result = await provider.DisconnectAsync(storageProvider, CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();

        var config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(storageProvider.Configuration!);
        await Assert.That(config).IsNotNull();

        // Tokens should be cleared
        await Assert.That(config!.RefreshToken).IsNull();
        await Assert.That(config.AccessToken).IsNull();
        await Assert.That(config.AccessTokenExpiry).IsNull();
        await Assert.That(config.GrantedScopes).IsNull();

        // Other fields should be preserved
        await Assert.That(config.ClientId).IsEqualTo(originalClientId);
        await Assert.That(config.EnableLocalCache).IsTrue();
    }
}
