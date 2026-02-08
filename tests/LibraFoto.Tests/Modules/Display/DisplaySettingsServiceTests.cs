using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Models;
using LibraFoto.Modules.Display.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display
{
    public class DisplaySettingsServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private DisplaySettingsService _service = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();
            _service = new DisplaySettingsService(_db, NullLogger<DisplaySettingsService>.Instance);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetActiveSettingsAsync_ReturnsActiveSettings_WhenExists()
        {
            // Arrange
            var settings1 = new DisplaySettings { Name = "Config 1", IsActive = false };
            var settings2 = new DisplaySettings { Name = "Config 2", IsActive = true };
            _db.DisplaySettings.AddRange(settings1, settings2);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetActiveSettingsAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("Config 2");
            await Assert.That(result.Id).IsEqualTo(settings2.Id);
        }

        [Test]
        public async Task GetActiveSettingsAsync_ReturnsFirstSettings_WhenNoActive()
        {
            // Arrange
            var settings1 = new DisplaySettings { Name = "Config 1", IsActive = false };
            var settings2 = new DisplaySettings { Name = "Config 2", IsActive = false };
            _db.DisplaySettings.AddRange(settings1, settings2);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetActiveSettingsAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("Config 1");
        }

        [Test]
        public async Task GetActiveSettingsAsync_CreatesDefault_WhenNoneExist()
        {
            // Act
            var result = await _service.GetActiveSettingsAsync();

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("Default");
            var dbSettings = await _db.DisplaySettings.FirstOrDefaultAsync();
            await Assert.That(dbSettings).IsNotNull();
            await Assert.That(dbSettings!.IsActive).IsTrue();
        }

        [Test]
        public async Task GetByIdAsync_ReturnsSettings_WhenExists()
        {
            // Arrange
            var settings = new DisplaySettings { Name = "Test Config" };
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("Test Config");
        }

        [Test]
        public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
        {
            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetAllAsync_ReturnsAllSettings_OrderedByName()
        {
            // Arrange
            _db.DisplaySettings.AddRange(
                new DisplaySettings { Name = "Zebra" },
                new DisplaySettings { Name = "Alpha" },
                new DisplaySettings { Name = "Beta" }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            await Assert.That(result.Count).IsEqualTo(3);
            await Assert.That(result[0].Name).IsEqualTo("Alpha");
            await Assert.That(result[1].Name).IsEqualTo("Beta");
            await Assert.That(result[2].Name).IsEqualTo("Zebra");
        }

        [Test]
        public async Task CreateAsync_CreatesNewSettings()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest
            {
                Name = "New Config",
                SlideDuration = 15,
                Shuffle = false,
                Transition = TransitionType.Slide
            };

            // Act
            var result = await _service.CreateAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("New Config");
            await Assert.That(result.SlideDuration).IsEqualTo(15);
            await Assert.That(result.Shuffle).IsFalse();
            await Assert.That(result.Transition).IsEqualTo(TransitionType.Slide);

            var dbSettings = await _db.DisplaySettings.FindAsync(result.Id);
            await Assert.That(dbSettings).IsNotNull();
        }

        [Test]
        public async Task CreateAsync_UsesDefaultName_WhenNotProvided()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest();

            // Act
            var result = await _service.CreateAsync(request);

            // Assert
            await Assert.That(result.Name).IsEqualTo("New Configuration");
        }

        [Test]
        public async Task UpdateAsync_UpdatesExistingSettings()
        {
            // Arrange
            var settings = new DisplaySettings { Name = "Original", SlideDuration = 10 };
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            var request = new UpdateDisplaySettingsRequest
            {
                Name = "Updated",
                SlideDuration = 20,
                Shuffle = true
            };

            // Act
            var result = await _service.UpdateAsync(settings.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("Updated");
            await Assert.That(result.SlideDuration).IsEqualTo(20);
            await Assert.That(result.Shuffle).IsTrue();
        }

        [Test]
        public async Task UpdateAsync_ReturnsNull_WhenNotExists()
        {
            // Arrange
            var request = new UpdateDisplaySettingsRequest { Name = "Test" };

            // Act
            var result = await _service.UpdateAsync(999, request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task DeleteAsync_DeletesSettings()
        {
            // Arrange
            _db.DisplaySettings.AddRange(
                new DisplaySettings { Name = "Config 1" },
                new DisplaySettings { Name = "Config 2" }
            );
            await _db.SaveChangesAsync();
            var toDelete = await _db.DisplaySettings.FirstAsync();

            // Act
            var result = await _service.DeleteAsync(toDelete.Id);

            // Assert
            await Assert.That(result).IsTrue();
            var remaining = await _db.DisplaySettings.CountAsync();
            await Assert.That(remaining).IsEqualTo(1);
        }

        [Test]
        public async Task DeleteAsync_ReturnsFalse_WhenNotExists()
        {
            // Act
            var result = await _service.DeleteAsync(999);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task DeleteAsync_ReturnsFalse_WhenLastSettings()
        {
            // Arrange
            var settings = new DisplaySettings { Name = "Only One" };
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.DeleteAsync(settings.Id);

            // Assert
            await Assert.That(result).IsFalse();
            var count = await _db.DisplaySettings.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        [Test]
        public async Task DeleteAsync_ActivatesAnother_WhenDeletingActive()
        {
            // Arrange
            var settings1 = new DisplaySettings { Name = "Config 1", IsActive = true };
            var settings2 = new DisplaySettings { Name = "Config 2", IsActive = false };
            _db.DisplaySettings.AddRange(settings1, settings2);
            await _db.SaveChangesAsync();

            // Act
            await _service.DeleteAsync(settings1.Id);

            // Assert
            await _db.Entry(settings2).ReloadAsync();
            await Assert.That(settings2.IsActive).IsTrue();
        }

        [Test]
        public async Task SetActiveAsync_ActivatesSettings()
        {
            // Arrange
            var settings1 = new DisplaySettings { Name = "Config 1", IsActive = true };
            var settings2 = new DisplaySettings { Name = "Config 2", IsActive = false };
            _db.DisplaySettings.AddRange(settings1, settings2);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.SetActiveAsync(settings2.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await _db.Entry(settings1).ReloadAsync();
            await _db.Entry(settings2).ReloadAsync();
            await Assert.That(settings1.IsActive).IsFalse();
            await Assert.That(settings2.IsActive).IsTrue();
        }

        [Test]
        public async Task SetActiveAsync_DeactivatesOthers()
        {
            // Arrange
            var settings1 = new DisplaySettings { Name = "Config 1", IsActive = true };
            var settings2 = new DisplaySettings { Name = "Config 2", IsActive = true };
            var settings3 = new DisplaySettings { Name = "Config 3", IsActive = false };
            _db.DisplaySettings.AddRange(settings1, settings2, settings3);
            await _db.SaveChangesAsync();

            // Act
            await _service.SetActiveAsync(settings3.Id);

            // Assert
            await _db.Entry(settings1).ReloadAsync();
            await _db.Entry(settings2).ReloadAsync();
            await _db.Entry(settings3).ReloadAsync();
            await Assert.That(settings1.IsActive).IsFalse();
            await Assert.That(settings2.IsActive).IsFalse();
            await Assert.That(settings3.IsActive).IsTrue();
        }

        [Test]
        public async Task SetActiveAsync_ReturnsNull_WhenNotExists()
        {
            // Act
            var result = await _service.SetActiveAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }
    }
}
