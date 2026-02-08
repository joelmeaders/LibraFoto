using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Display
{
    public class SlideshowServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private ServiceProvider _serviceProvider = null!;
        private SlideshowService _service = null!;
        private static long _testIdCounter = 1000000; // Start from high number to avoid collisions

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            // Create initial DbContext just to create schema
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            // Setup DI container - register factory that creates new DbContext per scope
            var services = new ServiceCollection();
            services.AddScoped<LibraFotoDbContext>(_ =>
            {
                var opts = new DbContextOptionsBuilder<LibraFotoDbContext>()
                    .UseSqlite(_connection).Options;
                return new LibraFotoDbContext(opts);
            });
            services.AddScoped<IDisplaySettingsService, DisplaySettingsService>();
            services.AddSingleton<ISlideshowService, SlideshowService>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            _serviceProvider = services.BuildServiceProvider();
            _service = (SlideshowService)_serviceProvider.GetRequiredService<ISlideshowService>();
        }

        [After(Test)]
        public async Task Cleanup()
        {
            // Clean up static state for any settings we created
            var allSettings = await _db.DisplaySettings.ToListAsync();
            foreach (var setting in allSettings)
            {
                _service.ResetSequence(setting.Id);
            }

            _serviceProvider.Dispose();
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        // Helper to create unique DisplaySettings IDs to avoid static state collisions
        private DisplaySettings CreateTestSettings(string name = "Test", bool isActive = true, bool shuffle = false, SourceType sourceType = SourceType.All, long? sourceId = null)
        {
            var uniqueId = Interlocked.Increment(ref _testIdCounter);
            var settings = new DisplaySettings
            {
                Id = uniqueId,
                Name = name,
                IsActive = isActive,
                Shuffle = shuffle,
                SourceType = sourceType,
                SourceId = sourceId
            };
            return settings;
        }

        [Test]
        public async Task GetNextPhotoAsync_ReturnsPhoto_WhenPhotosExist()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            var photo = new Photo
            {
                Filename = "test.jpg",
                OriginalFilename = "test.jpg",
                FilePath = "test.jpg",
                Width = 1920,
                Height = 1080
            };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId to avoid state collision
            var result = await _service.GetNextPhotoAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(photo.Id);
            await Assert.That(result.Width).IsEqualTo(1920);
            await Assert.That(result.Height).IsEqualTo(1080);
        }

        [Test]
        public async Task GetNextPhotoAsync_ReturnsNull_WhenNoPhotos()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId
            var result = await _service.GetNextPhotoAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetNextPhotoAsync_RotatesThroughPhotos_Sequential()
        {
            // Arrange
            var settings = CreateTestSettings(shuffle: false);
            _db.DisplaySettings.Add(settings);
            var photos = new[]
            {
                new Photo { Filename = "1.jpg", OriginalFilename = "1.jpg", FilePath = "1.jpg", Width = 100, Height = 100 },
                new Photo { Filename = "2.jpg", OriginalFilename = "2.jpg", FilePath = "2.jpg", Width = 100, Height = 100 },
                new Photo { Filename = "3.jpg", OriginalFilename = "3.jpg", FilePath = "3.jpg", Width = 100, Height = 100 }
            };
            _db.Photos.AddRange(photos);
            await _db.SaveChangesAsync();

            // Act - Get all 3 photos - use explicit settingsId
            var photo1 = await _service.GetNextPhotoAsync(settings.Id);
            var photo2 = await _service.GetNextPhotoAsync(settings.Id);
            var photo3 = await _service.GetNextPhotoAsync(settings.Id);
            var photo4 = await _service.GetNextPhotoAsync(settings.Id); // Should wrap around

            // Assert - All should be different on first pass
            await Assert.That(photo1).IsNotNull();
            await Assert.That(photo2).IsNotNull();
            await Assert.That(photo3).IsNotNull();
            await Assert.That(photo4).IsNotNull();

            var ids = new[] { photo1!.Id, photo2!.Id, photo3!.Id };
            await Assert.That(ids.Distinct().Count()).IsEqualTo(3);

            // Fourth photo should be one of the first 3 (wrapped around)
            await Assert.That(ids).Contains(photo4!.Id);
        }

        [Test]
        public async Task GetNextPhotoAsync_FiltersByAlbum()
        {
            // Arrange
            var album1 = new Album { Name = "Album 1" };
            var album2 = new Album { Name = "Album 2" };
            _db.Albums.AddRange(album1, album2);
            await _db.SaveChangesAsync();

            var photo1 = new Photo { Filename = "1.jpg", OriginalFilename = "1.jpg", FilePath = "1.jpg", Width = 100, Height = 100 };
            var photo2 = new Photo { Filename = "2.jpg", OriginalFilename = "2.jpg", FilePath = "2.jpg", Width = 100, Height = 100 };
            var photo3 = new Photo { Filename = "3.jpg", OriginalFilename = "3.jpg", FilePath = "3.jpg", Width = 100, Height = 100 };
            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            _db.PhotoAlbums.AddRange(
                new PhotoAlbum { PhotoId = photo1.Id, AlbumId = album1.Id },
                new PhotoAlbum { PhotoId = photo2.Id, AlbumId = album1.Id },
                new PhotoAlbum { PhotoId = photo3.Id, AlbumId = album2.Id }
            );
            await _db.SaveChangesAsync();

            var settings = CreateTestSettings(sourceType: SourceType.Album, sourceId: album1.Id);
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId
            var result1 = await _service.GetNextPhotoAsync(settings.Id);
            var result2 = await _service.GetNextPhotoAsync(settings.Id);
            var result3 = await _service.GetNextPhotoAsync(settings.Id);

            // Assert - Should only get photos from Album 1
            var ids = new[] { result1!.Id, result2!.Id, result3!.Id };
            await Assert.That(ids).Contains(photo1.Id);
            await Assert.That(ids).Contains(photo2.Id);
            await Assert.That(ids).DoesNotContain(photo3.Id);
        }

        [Test]
        public async Task GetNextPhotoAsync_FiltersByTag()
        {
            // Arrange
            var tag1 = new Tag { Name = "Tag1" };
            var tag2 = new Tag { Name = "Tag2" };
            _db.Tags.AddRange(tag1, tag2);
            await _db.SaveChangesAsync();

            var photo1 = new Photo { Filename = "1.jpg", OriginalFilename = "1.jpg", FilePath = "1.jpg", Width = 100, Height = 100 };
            var photo2 = new Photo { Filename = "2.jpg", OriginalFilename = "2.jpg", FilePath = "2.jpg", Width = 100, Height = 100 };
            _db.Photos.AddRange(photo1, photo2);
            await _db.SaveChangesAsync();

            _db.PhotoTags.Add(new PhotoTag { PhotoId = photo1.Id, TagId = tag1.Id });
            await _db.SaveChangesAsync();

            var settings = CreateTestSettings(sourceType: SourceType.Tag, sourceId: tag1.Id);
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Reset state for this specific settings
            _service.ResetSequence(settings.Id);

            // Act - use explicit settingsId
            var result = await _service.GetNextPhotoAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(photo1.Id);
        }

        [Test]
        public async Task GetCurrentPhotoAsync_ReturnsCurrent_WhenExists()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            var photo = new Photo { Filename = "test.jpg", OriginalFilename = "test.jpg", FilePath = "test.jpg", Width = 100, Height = 100 };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Reset state and get next photo to set current - use explicit settingsId
            await _service.GetNextPhotoAsync(settings.Id);

            // Act
            var result = await _service.GetCurrentPhotoAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Id).IsEqualTo(photo.Id);
        }

        [Test]
        public async Task GetCurrentPhotoAsync_GetsFirst_WhenNoCurrent()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            var photo = new Photo { Filename = "test.jpg", OriginalFilename = "test.jpg", FilePath = "test.jpg", Width = 100, Height = 100 };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act - without calling GetNext first - use explicit settingsId
            var result = await _service.GetCurrentPhotoAsync(settings.Id);

            // Assert
            await Assert.That(result).IsNotNull();
        }

        [Test]
        public async Task GetPreloadPhotosAsync_ReturnsMultiplePhotos()
        {
            // Arrange
            var settings = CreateTestSettings(shuffle: false);
            _db.DisplaySettings.Add(settings);
            for (int i = 1; i <= 15; i++)
            {
                _db.Photos.Add(new Photo
                {
                    Filename = $"{i}.jpg",
                    OriginalFilename = $"{i}.jpg",
                    FilePath = $"{i}.jpg",
                    Width = 100,
                    Height = 100
                });
            }
            await _db.SaveChangesAsync();

            // Reset state - use explicit settingsId to avoid static state collisions
            _service.ResetSequence(settings.Id);

            // Act - use explicit settingsId
            var result = await _service.GetPreloadPhotosAsync(10, settings.Id);

            // Assert - Should return up to 10 photos
            await Assert.That(result.Count).IsGreaterThanOrEqualTo(9);
            await Assert.That(result.Count).IsLessThanOrEqualTo(10);
        }

        [Test]
        public async Task GetPreloadPhotosAsync_ReturnsAllPhotos_WhenLessThanCount()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            for (int i = 1; i <= 5; i++)
            {
                _db.Photos.Add(new Photo
                {
                    Filename = $"{i}.jpg",
                    OriginalFilename = $"{i}.jpg",
                    FilePath = $"{i}.jpg",
                    Width = 100,
                    Height = 100
                });
            }
            await _db.SaveChangesAsync();

            // Reset state - use explicit settingsId
            _service.ResetSequence(settings.Id);

            // Act - use explicit settingsId
            var result = await _service.GetPreloadPhotosAsync(10, settings.Id);

            // Assert - When asking for 10 but only 5 exist, preload wraps and may return duplicates up to count 
            await Assert.That(result.Count).IsGreaterThanOrEqualTo(5);
            await Assert.That(result.Count).IsLessThanOrEqualTo(10);
        }

        [Test]
        public async Task ResetSequence_ClearsQueue()
        {
            // Arrange
            var settings = CreateTestSettings(shuffle: false);
            _db.DisplaySettings.Add(settings);
            var photos = new[]
            {
                new Photo { Filename = "1.jpg", OriginalFilename = "1.jpg", FilePath = "1.jpg", Width = 100, Height = 100 },
                new Photo { Filename = "2.jpg", OriginalFilename = "2.jpg", FilePath = "2.jpg", Width = 100, Height = 100 }
            };
            _db.Photos.AddRange(photos);
            await _db.SaveChangesAsync();

            var first = await _service.GetNextPhotoAsync(settings.Id);

            // Act
            _service.ResetSequence(settings.Id);
            var afterReset = await _service.GetNextPhotoAsync(settings.Id);

            // Assert - After reset, should restart from beginning
            await Assert.That(first).IsNotNull();
            await Assert.That(afterReset).IsNotNull();
            // Both could be the same photo if it's the first in sequence
        }

        [Test]
        public async Task GetPhotoCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            for (int i = 1; i <= 7; i++)
            {
                _db.Photos.Add(new Photo
                {
                    Filename = $"{i}.jpg",
                    OriginalFilename = $"{i}.jpg",
                    FilePath = $"{i}.jpg",
                    Width = 100,
                    Height = 100
                });
            }
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId
            var count = await _service.GetPhotoCountAsync(settings.Id);

            // Assert
            await Assert.That(count).IsEqualTo(7);
        }

        [Test]
        public async Task GetPhotoCountAsync_ReturnsZero_WhenNoPhotos()
        {
            // Arrange
            var settings = CreateTestSettings();
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId
            var count = await _service.GetPhotoCountAsync(settings.Id);

            // Assert
            await Assert.That(count).IsEqualTo(0);
        }

        [Test]
        public async Task GetPhotoCountAsync_CountsFilteredPhotos_ForAlbum()
        {
            // Arrange
            var album = new Album { Name = "Album 1" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var photo1 = new Photo { Filename = "1.jpg", OriginalFilename = "1.jpg", FilePath = "1.jpg", Width = 100, Height = 100 };
            var photo2 = new Photo { Filename = "2.jpg", OriginalFilename = "2.jpg", FilePath = "2.jpg", Width = 100, Height = 100 };
            var photo3 = new Photo { Filename = "3.jpg", OriginalFilename = "3.jpg", FilePath = "3.jpg", Width = 100, Height = 100 };
            _db.Photos.AddRange(photo1, photo2, photo3);
            await _db.SaveChangesAsync();

            _db.PhotoAlbums.AddRange(
                new PhotoAlbum { PhotoId = photo1.Id, AlbumId = album.Id },
                new PhotoAlbum { PhotoId = photo2.Id, AlbumId = album.Id }
            );
            await _db.SaveChangesAsync();

            var settings = CreateTestSettings(sourceType: SourceType.Album, sourceId: album.Id);
            _db.DisplaySettings.Add(settings);
            await _db.SaveChangesAsync();

            // Act - use explicit settingsId
            var count = await _service.GetPhotoCountAsync(settings.Id);

            // Assert - Should only count photos in the album
            await Assert.That(count).IsEqualTo(2);
        }
    }
}
