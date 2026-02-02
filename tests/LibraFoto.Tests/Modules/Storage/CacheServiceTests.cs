using LibraFoto.Data;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Storage;

public class CacheServiceTests
{
    private LibraFotoDbContext _dbContext = null!;
    private CacheService _cacheService = null!;
    private string _tempDir = null!;

    [Before(Test)]
    public async Task Setup()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new LibraFotoDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        // Create temp directory for cache
        _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Directory"] = _tempDir,
                ["Cache:MaxSizeBytes"] = (1024 * 1024).ToString() // 1MB for testing
            })
            .Build();

        _cacheService = new CacheService(_dbContext, config, NullLogger<CacheService>.Instance);

        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _dbContext.Dispose();

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task CacheFileAsync_CreatesCacheRecord()
    {
        // Arrange
        var fileContent = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(fileContent);
        var hash = await CacheService.ComputeHashAsync(stream, CancellationToken.None);
        stream.Position = 0;

        // Act
        var cachedFile = await _cacheService.CacheFileAsync(
            new CacheFileRequest
            {
                FileHash = hash,
                OriginalUrl = "https://example.com/file.jpg",
                ProviderId = 1,
                FileStream = stream,
                ContentType = "image/jpeg"
            },
            CancellationToken.None);

        // Assert
        await Assert.That(cachedFile).IsNotNull();
        await Assert.That(cachedFile.FileHash).IsEqualTo(hash);
        await Assert.That(File.Exists(cachedFile.LocalPath)).IsTrue();
    }

    [Test]
    public async Task GetCachedFileStreamAsync_ReturnsStreamForCachedFile()
    {
        // Arrange
        var fileContent = "Test content"u8.ToArray();
        using var stream = new MemoryStream(fileContent);
        var hash = await CacheService.ComputeHashAsync(stream, CancellationToken.None);
        stream.Position = 0;

        await _cacheService.CacheFileAsync(
            new CacheFileRequest
            {
                FileHash = hash,
                OriginalUrl = "https://example.com/test.jpg",
                ProviderId = 1,
                FileStream = stream,
                ContentType = "image/jpeg"
            },
            CancellationToken.None);

        // Act
        using var resultStream = await _cacheService.GetCachedFileStreamAsync(hash, CancellationToken.None);

        // Assert
        await Assert.That(resultStream).IsNotNull();
        using var reader = new StreamReader(resultStream!);
        var content = await reader.ReadToEndAsync();
        await Assert.That(content).IsEqualTo("Test content");
    }

    [Test]
    public async Task EvictLRUAsync_RemovesOldestFiles()
    {
        // Arrange - cache 3 files
        await CacheFileHelper("file1.jpg", "Content 1");
        await Task.Delay(10); // Ensure different timestamps
        await CacheFileHelper("file2.jpg", "Content 2");
        await Task.Delay(10);
        await CacheFileHelper("file3.jpg", "Content 3");

        var initialCount = await _cacheService.GetCacheCountAsync(CancellationToken.None);
        await Assert.That(initialCount).IsEqualTo(3);

        // Act - evict to a very small size
        await _cacheService.EvictLRUAsync(10, CancellationToken.None);

        // Assert
        var remaining = await _cacheService.GetCacheCountAsync(CancellationToken.None);
        await Assert.That(remaining).IsLessThan(3);
    }

    [Test]
    public async Task ClearCacheAsync_RemovesAllFiles()
    {
        // Arrange
        await CacheFileHelper("file1.jpg", "Content 1");
        await CacheFileHelper("file2.jpg", "Content 2");

        var initialCount = await _cacheService.GetCacheCountAsync(CancellationToken.None);
        await Assert.That(initialCount).IsEqualTo(2);

        // Act
        await _cacheService.ClearCacheAsync(CancellationToken.None);

        // Assert
        var count = await _cacheService.GetCacheCountAsync(CancellationToken.None);
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task GetCacheSizeAsync_ReturnsCorrectSize()
    {
        // Arrange
        var content1 = "Content 1";
        var content2 = "Content 2 is longer";
        await CacheFileHelper("file1.jpg", content1);
        await CacheFileHelper("file2.jpg", content2);

        // Act
        var totalSize = await _cacheService.GetCacheSizeAsync(CancellationToken.None);

        // Assert
        var expectedSize = System.Text.Encoding.UTF8.GetByteCount(content1) + System.Text.Encoding.UTF8.GetByteCount(content2);
        await Assert.That(totalSize).IsEqualTo(expectedSize);
    }

    [Test]
    public async Task CacheFileAsync_DoesNotDuplicateExistingFile()
    {
        // Arrange
        var fileContent = "Same content"u8.ToArray();
        using var stream1 = new MemoryStream(fileContent);
        var hash = await CacheService.ComputeHashAsync(stream1, CancellationToken.None);
        stream1.Position = 0;

        // Act - cache the same file twice
        await _cacheService.CacheFileAsync(
            new CacheFileRequest
            {
                FileHash = hash,
                OriginalUrl = "https://example.com/file1.jpg",
                ProviderId = 1,
                FileStream = stream1,
                ContentType = "image/jpeg"
            },
            CancellationToken.None);

        using var stream2 = new MemoryStream(fileContent);
        stream2.Position = 0;
        await _cacheService.CacheFileAsync(
            new CacheFileRequest
            {
                FileHash = hash,
                OriginalUrl = "https://example.com/file2.jpg",
                ProviderId = 1,
                FileStream = stream2,
                ContentType = "image/jpeg"
            },
            CancellationToken.None);

        // Assert
        var count = await _cacheService.GetCacheCountAsync(CancellationToken.None);
        await Assert.That(count).IsEqualTo(1); // Should only have one cached file
    }

    private async Task<string> CacheFileHelper(string filename, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes);
        var hash = await CacheService.ComputeHashAsync(stream, CancellationToken.None);
        stream.Position = 0;

        await _cacheService.CacheFileAsync(
            new CacheFileRequest
            {
                FileHash = hash,
                OriginalUrl = $"https://example.com/{filename}",
                ProviderId = 1,
                FileStream = stream,
                ContentType = "image/jpeg"
            },
            CancellationToken.None);
        return hash;
    }
}
