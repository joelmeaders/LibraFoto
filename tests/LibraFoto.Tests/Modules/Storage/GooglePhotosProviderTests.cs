using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.Tests.Modules.Storage
{
    /// <summary>
    /// Tests for GooglePhotosProvider using the Picker API model.
    /// Note: Photos are imported via the Picker flow and stored locally.
    /// </summary>
    public class GooglePhotosProviderTests
    {
        private GooglePhotosProvider _provider = null!;
        private TestHttpClientFactory _httpClientFactory = null!;
        private LibraFotoDbContext _dbContext = null!;

        [Before(Test)]
        public async Task Setup()
        {
            var logger = NullLogger<GooglePhotosProvider>.Instance;

            _httpClientFactory = new TestHttpClientFactory();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new LibraFotoDbContext(options);
            await _dbContext.Database.EnsureCreatedAsync();

            _provider = new GooglePhotosProvider(logger, _httpClientFactory, _dbContext);

            await Task.CompletedTask;
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _dbContext.DisposeAsync();
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new HttpClient();
        }

        #region Properties Tests

        [Test]
        public async Task ProviderId_ReturnsInitializedId()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-secret"
            };

            _provider.Initialize(42, "Test Google Photos", JsonSerializer.Serialize(config));

            await Assert.That(_provider.ProviderId).IsEqualTo(42);
        }

        [Test]
        public async Task DisplayName_ReturnsInitializedName()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-secret"
            };

            _provider.Initialize(1, "My Google Photos", JsonSerializer.Serialize(config));

            await Assert.That(_provider.DisplayName).IsEqualTo("My Google Photos");
        }

        [Test]
        public async Task ProviderType_ReturnsGooglePhotos()
        {
            await Assert.That(_provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task SupportsUpload_ReturnsFalse()
        {
            await Assert.That(_provider.SupportsUpload).IsFalse();
        }

        [Test]
        public async Task SupportsWatch_ReturnsFalse()
        {
            await Assert.That(_provider.SupportsWatch).IsFalse();
        }

        #endregion

        #region Initialization Tests

        [Test]
        public async Task Initialize_WithValidConfiguration_Succeeds()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-client-id",
                ClientSecret = "test-secret",
                RefreshToken = "test-refresh-token"
            };

            _provider.Initialize(1, "Test Provider", JsonSerializer.Serialize(config));

            await Assert.That(_provider.ProviderId).IsEqualTo(1);
            await Assert.That(_provider.DisplayName).IsEqualTo("Test Provider");
        }

        [Test]
        public async Task Initialize_WithEmptyConfiguration_UsesDefaults()
        {
            _provider.Initialize(1, "Test Provider", null);

            await Assert.That(_provider.ProviderId).IsEqualTo(1);
            await Assert.That(_provider.ProviderType).IsEqualTo(StorageProviderType.GooglePhotos);
        }

        [Test]
        public async Task Initialize_WithInvalidJson_UsesDefaults()
        {
            _provider.Initialize(1, "Test Provider", "{ invalid json }");

            await Assert.That(_provider.ProviderId).IsEqualTo(1);
        }

        #endregion

        #region Read-Only Operations Tests

        [Test]
        public async Task UploadFileAsync_ThrowsNotSupportedException()
        {
            using var stream = new MemoryStream(new byte[100]);

            await Assert.That(async () =>
                await _provider.UploadFileAsync("test.jpg", stream, "image/jpeg"))
                .Throws<NotSupportedException>().WithMessageContaining("read-only");
        }

        [Test]
        public async Task DeleteFileAsync_ThrowsNotSupportedException()
        {
            await Assert.That(async () =>
                await _provider.DeleteFileAsync("test-file-id"))
                .Throws<NotSupportedException>();
        }

        #endregion

        #region GetFilesAsync Tests

        [Test]
        public async Task GetFilesAsync_WithNoPhotos_ReturnsEmptyList()
        {
            _provider.Initialize(1, "Test Provider", null);

            var files = await _provider.GetFilesAsync(null);

            await Assert.That(files).IsEmpty();
        }

        [Test]
        public async Task GetFilesAsync_WithImportedPhotos_ReturnsPhotos()
        {
            _provider.Initialize(1, "Test Provider", null);

            // Add a photo to the database that was imported via Picker
            _dbContext.Photos.Add(new Photo
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
            await _dbContext.SaveChangesAsync();

            var files = await _provider.GetFilesAsync(null);

            await Assert.That(files.Count()).IsEqualTo(1);
            await Assert.That(files.First().FileId).IsEqualTo("google-file-id-123");
        }

        #endregion

        #region TestConnectionAsync Tests

        [Test]
        public async Task TestConnectionAsync_WithoutCredentials_ReturnsFalse()
        {
            var config = new GooglePhotosConfiguration();
            _provider.Initialize(1, "Test Provider", JsonSerializer.Serialize(config));

            var result = await _provider.TestConnectionAsync();

            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task TestConnectionAsync_WithIncompleteCredentials_ReturnsFalse()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-id"
            };
            _provider.Initialize(1, "Test Provider", JsonSerializer.Serialize(config));

            var result = await _provider.TestConnectionAsync();

            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task TestConnectionAsync_WithPickerScope_ReturnsTrue()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-id",
                ClientSecret = "test-secret",
                RefreshToken = "test-refresh",
                GrantedScopes = [GooglePhotosProvider.PickerScope]
            };
            _provider.Initialize(1, "Test Provider", JsonSerializer.Serialize(config));

            var result = await _provider.TestConnectionAsync();

            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task TestConnectionAsync_WithWrongScope_ReturnsFalse()
        {
            var config = new GooglePhotosConfiguration
            {
                ClientId = "test-id",
                ClientSecret = "test-secret",
                RefreshToken = "test-refresh",
                GrantedScopes = ["https://www.googleapis.com/auth/photoslibrary.readonly"]
            };
            _provider.Initialize(1, "Test Provider", JsonSerializer.Serialize(config));

            var result = await _provider.TestConnectionAsync();

            await Assert.That(result).IsFalse();
        }

        #endregion

        #region Configuration Tests

        #endregion

        #region FileExistsAsync Tests

        [Test]
        public async Task FileExistsAsync_WithNoPhotos_ReturnsFalse()
        {
            _provider.Initialize(1, "Test Provider", null);

            var exists = await _provider.FileExistsAsync("nonexistent-file");

            await Assert.That(exists).IsFalse();
        }

        [Test]
        public async Task FileExistsAsync_WithImportedPhoto_ReturnsTrue()
        {
            _provider.Initialize(1, "Test Provider", null);

            _dbContext.Photos.Add(new Photo
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
            await _dbContext.SaveChangesAsync();

            var exists = await _provider.FileExistsAsync("google-file-id-123");

            await Assert.That(exists).IsTrue();
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
    }
}
