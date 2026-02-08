using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.GooglePhotos.Tests
{
    /// <summary>
    /// Comprehensive tests for GooglePhotosProvider using the Picker API model.
    /// Photos are selected by users through Google's Picker UI and imported locally.
    /// </summary>
    public class GooglePhotosProviderTests
    {
        private const string TestClientId = "test-client-id";
        private const string TestClientSecret = "test-secret";
        private const string TestProviderName = "Test Provider";
        private const string TestFileId = "test-file-id";

        private static async Task<(GooglePhotosProvider provider, LibraFotoDbContext dbContext)> CreateProviderAsync()
        {
            var logger = NullLogger<GooglePhotosProvider>.Instance;
            var httpClientFactory = new TestHttpClientFactory();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;
            var dbContext = new LibraFotoDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var provider = new GooglePhotosProvider(logger, httpClientFactory, dbContext);
            return (provider, dbContext);
        }

        #region Helper Classes

        private sealed class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new HttpClient();
        }

        #endregion

        #region Properties Tests

        [Test]
        public async Task ProviderId_ReturnsInitializedId()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret
            };

            provider.Initialize(42, "Test Google Photos", JsonSerializer.Serialize(config));

            await Assert.That(provider.ProviderId).IsEqualTo(42);
        }

        [Test]
        public async Task DisplayName_ReturnsInitializedName()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret
            };

            provider.Initialize(1, "My Google Photos", JsonSerializer.Serialize(config));

            await Assert.That(provider.DisplayName).IsEqualTo("My Google Photos");
        }

        [Test]
        public async Task ProviderType_ReturnsGooglePhotos()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task SupportsUpload_ReturnsFalse()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(provider.SupportsUpload).IsFalse();
        }

        [Test]
        public async Task SupportsWatch_ReturnsFalse()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(provider.SupportsWatch).IsFalse();
        }

        #endregion

        #region Initialization Tests

        [Test]
        public async Task Initialize_WithValidConfiguration_Succeeds()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret,
                RefreshToken = "test-refresh-token"
            };

            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            await Assert.That(provider.ProviderId).IsEqualTo(1);
            await Assert.That(provider.DisplayName).IsEqualTo(TestProviderName);
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task Initialize_WithEmptyConfiguration_UsesDefaults()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            provider.Initialize(1, TestProviderName, null);

            await Assert.That(provider.ProviderId).IsEqualTo(1);
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task Initialize_WithInvalidJson_UsesDefaults()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            provider.Initialize(1, TestProviderName, "{ invalid json }");

            await Assert.That(provider.ProviderId).IsEqualTo(1);
            await Assert.That(provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        #endregion

        #region Read-Only Operations Tests

        [Test]
        public async Task UploadFileAsync_ThrowsNotSupportedException()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            using var stream = new MemoryStream(new byte[100]);

            await Assert.That(async () =>
                await provider.UploadFileAsync("test.jpg", stream, "image/jpeg"))
                .Throws<NotSupportedException>().WithMessageContaining("read-only");
        }

        [Test]
        public async Task DeleteFileAsync_ThrowsNotSupportedException()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(async () =>
                await provider.DeleteFileAsync(TestFileId))
                .Throws<NotSupportedException>();
        }

        #endregion

        #region GetFilesAsync Tests

        [Test]
        public async Task GetFilesAsync_WithNoPhotos_ReturnsEmptyList()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret
            };

            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            var files = await provider.GetFilesAsync(null);

            await Assert.That(files).IsEmpty();
        }

        [Test]
        public async Task GetFilesAsync_WithImportedPhotos_ReturnsPhotos()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            provider.Initialize(1, TestProviderName, null);

            dbContext.Photos.Add(new Photo
            {
                Id = 1,
                Filename = "test.jpg",
                OriginalFilename = "test.jpg",
                FilePath = "/path/to/test.jpg",
                FileSize = 1000,
                Width = 800,
                Height = 600,
                MediaType = MediaType.Photo,
                ProviderId = 1,
                ProviderFileId = "google-file-id-123"
            });
            await dbContext.SaveChangesAsync();

            var files = await provider.GetFilesAsync(null);

            await Assert.That(files.Count()).IsEqualTo(1);
        }

        #endregion

        #region TestConnectionAsync Tests

        [Test]
        public async Task TestConnectionAsync_WithoutCredentials_ReturnsFalse()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration();
            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            var result = await provider.TestConnectionAsync();

            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task TestConnectionAsync_WithIncompleteCredentials_ReturnsFalse()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId
            };
            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            var result = await provider.TestConnectionAsync();

            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task TestConnectionAsync_WithPickerScope_ReturnsTrue()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret,
                RefreshToken = "test-refresh",
                GrantedScopes = [GooglePhotosProvider.PickerScope]
            };
            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            var result = await provider.TestConnectionAsync();

            await Assert.That(result).IsTrue();
        }

        #endregion

        #region Configuration Tests

        #endregion

        #region FileExistsAsync Tests

        [Test]
        public async Task FileExistsAsync_WithNoPhotos_ReturnsFalse()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            var config = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret
            };
            provider.Initialize(1, TestProviderName, JsonSerializer.Serialize(config));

            var exists = await provider.FileExistsAsync(TestFileId);

            await Assert.That(exists).IsFalse();
        }

        [Test]
        public async Task FileExistsAsync_WithImportedPhoto_ReturnsTrue()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            provider.Initialize(1, TestProviderName, null);

            dbContext.Photos.Add(new Photo
            {
                Id = 1,
                Filename = "test.jpg",
                OriginalFilename = "test.jpg",
                FilePath = "/path/to/test.jpg",
                FileSize = 1000,
                Width = 800,
                Height = 600,
                MediaType = MediaType.Photo,
                ProviderId = 1,
                ProviderFileId = "google-file-id-123"
            });
            await dbContext.SaveChangesAsync();

            var exists = await provider.FileExistsAsync("google-file-id-123");

            await Assert.That(exists).IsTrue();
        }

        #endregion

        #region Multiple Provider Instances Tests

        [Test]
        public async Task MultipleProviders_CanBeInitializedIndependently()
        {
            var (provider1, dbContext1) = await CreateProviderAsync();
            var (provider2, dbContext2) = await CreateProviderAsync();
            await using var _1 = dbContext1;
            await using var _2 = dbContext2;

            var config1 = new GooglePhotosConfiguration { ClientId = "id1", ClientSecret = "secret1" };
            var config2 = new GooglePhotosConfiguration { ClientId = "id2", ClientSecret = "secret2" };

            provider1.Initialize(1, "Provider 1", JsonSerializer.Serialize(config1));
            provider2.Initialize(2, "Provider 2", JsonSerializer.Serialize(config2));

            await Assert.That(provider1.ProviderId).IsEqualTo(1);
            await Assert.That(provider2.ProviderId).IsEqualTo(2);
            await Assert.That(provider1.DisplayName).IsEqualTo("Provider 1");
            await Assert.That(provider2.DisplayName).IsEqualTo("Provider 2");
        }

        #endregion

        #region Provider Capability Tests

        [Test]
        public async Task Provider_ImplementsIStorageProvider()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(provider).IsAssignableTo<IStorageProvider>();
        }

        [Test]
        public async Task Provider_IsReadOnly()
        {
            var (provider, dbContext) = await CreateProviderAsync();
            await using var _ = dbContext;

            await Assert.That(provider.SupportsUpload).IsFalse();

            using var stream = new MemoryStream();
            await Assert.That(async () => await provider.UploadFileAsync("test.jpg", stream, "image/jpeg"))
                .Throws<NotSupportedException>();

            await Assert.That(async () => await provider.DeleteFileAsync(TestFileId))
                .Throws<NotSupportedException>();
        }

        #endregion

        #region HasRequiredScopes Tests

        [Test]
        public async Task HasRequiredScopes_WithPickerScope_ReturnsTrue()
        {
            var scopes = new[] { GooglePhotosProvider.PickerScope };

            var result = GooglePhotosProvider.HasRequiredScopes(scopes);

            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task HasRequiredScopes_WithoutPickerScope_ReturnsFalse()
        {
            var scopes = new[] { "https://www.googleapis.com/auth/photoslibrary.readonly" };

            var result = GooglePhotosProvider.HasRequiredScopes(scopes);

            await Assert.That(result).IsFalse();
        }

        #endregion

        #region Configuration Serialization Tests

        [Test]
        public async Task Configuration_CanBeSerializedAndDeserialized()
        {
            var originalConfig = new GooglePhotosConfiguration
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret,
                RefreshToken = "test-refresh",
                AccessToken = "test-access",
                AccessTokenExpiry = DateTime.UtcNow.AddHours(1)
            };

            var json = JsonSerializer.Serialize(originalConfig);
            var deserializedConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(json);

            await Assert.That(deserializedConfig).IsNotNull();
            await Assert.That(deserializedConfig!.ClientId).IsEqualTo(originalConfig.ClientId);
            await Assert.That(deserializedConfig.ClientSecret).IsEqualTo(originalConfig.ClientSecret);
        }

        #endregion
    }
}
