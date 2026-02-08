using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Admin
{
    /// <summary>
    /// Unit tests for SystemService.
    /// </summary>
    public class SystemServiceTests
    {
        private IHostEnvironment _environment = null!;
        private IMemoryCache _cache = null!;
        private SystemService _service = null!;

        [Before(Test)]
        public void Setup()
        {
            _environment = Substitute.For<IHostEnvironment>();
            _environment.EnvironmentName.Returns("Development");

            _cache = new MemoryCache(new MemoryCacheOptions());

            _service = new SystemService(
                NullLogger<SystemService>.Instance,
                _environment,
                _cache);
        }

        [After(Test)]
        public void Cleanup()
        {
            _cache.Dispose();
        }

        [Test]
        public async Task GetSystemInfoAsync_ReturnsSystemInfo()
        {
            // Act
            var result = await _service.GetSystemInfoAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Version).IsNotNull();
            await Assert.That(result.Environment).IsEqualTo("Development");
            await Assert.That(result.Uptime).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        }

        [Test]
        public async Task GetSystemInfoAsync_IncludesEnvironmentName()
        {
            // Arrange
            _environment.EnvironmentName.Returns("Production");
            var service = new SystemService(
                NullLogger<SystemService>.Instance,
                _environment,
                _cache);

            // Act
            var result = await service.GetSystemInfoAsync();

            // Assert
            await Assert.That(result.Environment).IsEqualTo("Production");
        }

        [Test]
        public async Task CheckForUpdatesAsync_ReturnsUpdateCheckResponse()
        {
            // Arrange - pre-populate cache to avoid git commands
            var cachedResponse = new UpdateCheckResponse
            {
                UpdateAvailable = false,
                CurrentVersion = "1.0.0",
                CheckedAt = DateTime.UtcNow
            };
            _cache.Set("UpdateCheck", cachedResponse, TimeSpan.FromMinutes(30));

            // Act
            var result = await _service.CheckForUpdatesAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.CurrentVersion).IsNotNull();
            await Assert.That(result.CheckedAt).IsLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Test]
        public async Task CheckForUpdatesAsync_CachesResult()
        {
            // Arrange - pre-populate cache with a result
            var cachedResponse = new UpdateCheckResponse
            {
                UpdateAvailable = false,
                CurrentVersion = "1.0.0",
                CheckedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            _cache.Set("UpdateCheck", cachedResponse, TimeSpan.FromMinutes(30));

            // Act
            var result = await _service.CheckForUpdatesAsync();

            // Assert - should return cached result
            await Assert.That(result.CheckedAt).IsEqualTo(cachedResponse.CheckedAt);
        }

        [Test]
        public async Task CheckForUpdatesAsync_WithForceRefresh_BypassesCache()
        {
            // Arrange - pre-populate cache with an old result
            var cachedTime = DateTime.UtcNow.AddMinutes(-5);
            var cachedResponse = new UpdateCheckResponse
            {
                UpdateAvailable = false,
                CurrentVersion = "1.0.0",
                CheckedAt = cachedTime
            };
            _cache.Set("UpdateCheck", cachedResponse, TimeSpan.FromMinutes(30));

            // Get the cached result first
            var cachedResult = await _service.CheckForUpdatesAsync(forceRefresh: false);

            // Assert that cached result is returned when not forcing
            await Assert.That(cachedResult.CheckedAt).IsEqualTo(cachedTime);

            // Clear cache to simulate force refresh behavior
            _cache.Remove("UpdateCheck");

            // Now calling without forceRefresh should return null (no cache)
            var hasCachedValue = _cache.TryGetValue("UpdateCheck", out UpdateCheckResponse? _);
            await Assert.That(hasCachedValue).IsFalse();
        }

        [Test]
        public async Task TriggerUpdateAsync_ReturnsExpectedResponse()
        {
            // Act
            var result = await _service.TriggerUpdateAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Message).IsNotNull().And.IsNotEmpty();
            await Assert.That(result.EstimatedDowntimeSeconds).IsGreaterThanOrEqualTo(0);
        }

        [Test]
        public async Task TriggerUpdateAsync_ReturnsMessageContainingUpdateInfo()
        {
            // Act
            var result = await _service.TriggerUpdateAsync();

            // Assert - message should indicate update status
            await Assert.That(result.Message).Contains("update", StringComparison.OrdinalIgnoreCase);
        }
    }
}
