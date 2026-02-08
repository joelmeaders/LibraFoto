using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Tests.Modules.Admin
{
    /// <summary>
    /// Tests for TagService.
    /// </summary>
    public class TagServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private TagService _service = null!;

        [Before(Test)]
        public async Task Setup()
        {
            // Use unique database for each test to avoid concurrency issues
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .Options;

            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            _service = new TagService(_db);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetTagsAsync_WithNoTags_ReturnsEmptyList()
        {
            // Act
            var result = await _service.GetTagsAsync();

            // Assert
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task GetTagsAsync_OrdersByName()
        {
            // Arrange
            _db.Tags.AddRange(
                new Tag { Name = "C Tag", Color = "#ff0000" },
                new Tag { Name = "A Tag", Color = "#00ff00" },
                new Tag { Name = "B Tag", Color = "#0000ff" }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetTagsAsync();

            // Assert
            await Assert.That(result[0].Name).IsEqualTo("A Tag");
            await Assert.That(result[1].Name).IsEqualTo("B Tag");
            await Assert.That(result[2].Name).IsEqualTo("C Tag");
        }

        [Test]
        public async Task CreateTagAsync_CreatesNewTag()
        {
            // Arrange
            var request = new CreateTagRequest("Nature", "#00ff00");

            // Act
            var result = await _service.CreateTagAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("Nature");
            await Assert.That(result.Color).IsEqualTo("#00ff00");
            await Assert.That(result.PhotoCount).IsZero();

            var dbTag = await _db.Tags.FirstAsync();
            await Assert.That(dbTag.Name).IsEqualTo("Nature");
        }

        [Test]
        public async Task GetTagByIdAsync_WithExistingId_ReturnsTag()
        {
            // Arrange
            var tag = new Tag { Name = "Test Tag", Color = "#ff0000" };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetTagByIdAsync(tag.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("Test Tag");
            await Assert.That(result.Color).IsEqualTo("#ff0000");
        }

        [Test]
        public async Task GetTagByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var result = await _service.GetTagByIdAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdateTagAsync_UpdatesAllFields()
        {
            // Arrange
            var tag = new Tag { Name = "Old Name", Color = "#000000" };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            var request = new UpdateTagRequest("New Name", "#ffffff");

            // Act
            var result = await _service.UpdateTagAsync(tag.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("New Name");
            await Assert.That(result.Color).IsEqualTo("#ffffff");
        }

        [Test]
        public async Task UpdateTagAsync_WithPartialUpdate_OnlyUpdatesProvidedFields()
        {
            // Arrange
            var tag = new Tag { Name = "Original", Color = "#000000" };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            var request = new UpdateTagRequest(Name: "Updated", Color: null);

            // Act
            var result = await _service.UpdateTagAsync(tag.Id, request);

            // Assert
            await Assert.That(result!.Name).IsEqualTo("Updated");
            await Assert.That(result.Color).IsEqualTo("#000000");
        }

        [Test]
        public async Task UpdateTagAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var request = new UpdateTagRequest("Name", "#ff0000");

            // Act
            var result = await _service.UpdateTagAsync(999, request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task DeleteTagAsync_WithExistingTag_DeletesAndReturnsTrue()
        {
            // Arrange
            var tag = new Tag { Name = "Test", Color = "#ff0000" };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.DeleteTagAsync(tag.Id);

            // Assert
            await Assert.That(result).IsTrue();

            var deletedTag = await _db.Tags.FindAsync(tag.Id);
            await Assert.That(deletedTag).IsNull();
        }

        [Test]
        public async Task DeleteTagAsync_WithNonExistentTag_ReturnsFalse()
        {
            // Act
            var result = await _service.DeleteTagAsync(999);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task AddPhotosAsync_WithValidIds_AddsPhotoTags()
        {
            // Arrange
            var tag = new Tag { Name = "Test", Color = "#ff0000" };
            var photos = new[]
            {
                new Photo { Filename = "photo1.jpg", FilePath = "photo1.jpg", FileSize = 100 },
                new Photo { Filename = "photo2.jpg", FilePath = "photo2.jpg", FileSize = 100 }
            };
            _db.Tags.Add(tag);
            _db.Photos.AddRange(photos);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosAsync(tag.Id, photos.Select(p => p.Id).ToArray());

            // Assert
            await Assert.That(result.SuccessCount).IsEqualTo(2);
            await Assert.That(result.FailedCount).IsZero();

            var photoTags = await _db.PhotoTags.CountAsync();
            await Assert.That(photoTags).IsEqualTo(2);
        }

        [Test]
        public async Task AddPhotosAsync_WithInvalidTagId_ReturnsError()
        {
            // Arrange
            var photo = new Photo { Filename = "photo.jpg", FilePath = "photo.jpg", FileSize = 100 };
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosAsync(999, [photo.Id]);

            // Assert
            await Assert.That(result.SuccessCount).IsZero();
            await Assert.That(result.FailedCount).IsPositive();
            await Assert.That(result.Errors).IsNotEmpty();
        }

        [Test]
        public async Task AddPhotosAsync_WithInvalidPhotoIds_ReturnsErrors()
        {
            // Arrange
            var tag = new Tag { Name = "Test", Color = "#ff0000" };
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosAsync(tag.Id, [999, 888]);

            // Assert
            await Assert.That(result.SuccessCount).IsZero();
            await Assert.That(result.FailedCount).IsEqualTo(2);
            await Assert.That(result.Errors.Length).IsEqualTo(2);
        }

        [Test]
        public async Task AddPhotosAsync_WithDuplicatePhotoTag_ReturnsError()
        {
            // Arrange
            var tag = new Tag { Name = "Test", Color = "#ff0000" };
            var photo = new Photo { Filename = "photo.jpg", FilePath = "photo.jpg", FileSize = 100 };
            _db.Tags.Add(tag);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            _db.PhotoTags.Add(new PhotoTag { TagId = tag.Id, PhotoId = photo.Id, DateAdded = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.AddPhotosAsync(tag.Id, [photo.Id]);

            // Assert
            await Assert.That(result.SuccessCount).IsZero();
            await Assert.That(result.FailedCount).IsPositive();
        }

        [Test]
        public async Task RemovePhotosAsync_WithValidIds_RemovesPhotoTags()
        {
            // Arrange
            var tag = new Tag { Name = "Test", Color = "#ff0000" };
            var photo = new Photo { Filename = "photo.jpg", FilePath = "photo.jpg", FileSize = 100 };
            _db.Tags.Add(tag);
            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            _db.PhotoTags.Add(new PhotoTag { TagId = tag.Id, PhotoId = photo.Id, DateAdded = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.RemovePhotosAsync(tag.Id, [photo.Id]);

            // Assert
            await Assert.That(result.SuccessCount).IsPositive();
            await Assert.That(result.FailedCount).IsZero();

            var photoTags = await _db.PhotoTags.CountAsync();
            await Assert.That(photoTags).IsZero();
        }
    }
}
