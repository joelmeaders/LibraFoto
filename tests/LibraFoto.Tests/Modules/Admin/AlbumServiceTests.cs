using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Tests.Modules.Admin
{
    /// <summary>
    /// Tests for AlbumService.
    /// </summary>
    public class AlbumServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private AlbumService _service = null!;

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

            _service = new AlbumService(_db);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetAlbumsAsync_WithNoAlbums_ReturnsEmptyList()
        {
            // Act
            var result = await _service.GetAlbumsAsync();

            // Assert
            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task GetAlbumsAsync_OrdersBySortOrderThenName()
        {
            // Arrange
            _db.Albums.AddRange(
                new Album { Name = "C Album", SortOrder = 2 },
                new Album { Name = "A Album", SortOrder = 1 },
                new Album { Name = "B Album", SortOrder = 1 }
            );
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetAlbumsAsync();

            // Assert
            await Assert.That(result[0].Name).IsEqualTo("A Album");
            await Assert.That(result[1].Name).IsEqualTo("B Album");
            await Assert.That(result[2].Name).IsEqualTo("C Album");
        }

        [Test]
        public async Task CreateAlbumAsync_CreatesNewAlbum()
        {
            // Arrange
            var request = new CreateAlbumRequest("My Album", "Description");

            // Act
            var result = await _service.CreateAlbumAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Name).IsEqualTo("My Album");
            await Assert.That(result.Description).IsEqualTo("Description");
            await Assert.That(result.SortOrder).IsEqualTo(1);

            var dbAlbum = await _db.Albums.FirstAsync();
            await Assert.That(dbAlbum.Name).IsEqualTo("My Album");
        }

        [Test]
        public async Task CreateAlbumAsync_IncrementsSortOrder()
        {
            // Arrange
            _db.Albums.Add(new Album { Name = "First", SortOrder = 5 });
            await _db.SaveChangesAsync();

            var request = new CreateAlbumRequest("Second", null);

            // Act
            var result = await _service.CreateAlbumAsync(request);

            // Assert
            await Assert.That(result.SortOrder).IsEqualTo(6);
        }

        [Test]
        public async Task GetAlbumByIdAsync_WithExistingId_ReturnsAlbum()
        {
            // Arrange
            var album = new Album { Name = "Test Album" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetAlbumByIdAsync(album.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("Test Album");
        }

        [Test]
        public async Task GetAlbumByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var result = await _service.GetAlbumByIdAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdateAlbumAsync_UpdatesAllFields()
        {
            // Arrange
            var album = new Album { Name = "Old Name", Description = "Old Desc", SortOrder = 1 };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var request = new UpdateAlbumRequest("New Name", "New Desc", null, 5);

            // Act
            var result = await _service.UpdateAlbumAsync(album.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Name).IsEqualTo("New Name");
            await Assert.That(result.Description).IsEqualTo("New Desc");
            await Assert.That(result.SortOrder).IsEqualTo(5);
        }

        [Test]
        public async Task UpdateAlbumAsync_WithPartialUpdate_OnlyUpdatesProvidedFields()
        {
            // Arrange
            var album = new Album { Name = "Original", Description = "Original Desc", SortOrder = 1 };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            var request = new UpdateAlbumRequest(Name: "Updated Name", Description: null, CoverPhotoId: null, SortOrder: null);

            // Act
            var result = await _service.UpdateAlbumAsync(album.Id, request);

            // Assert
            await Assert.That(result!.Name).IsEqualTo("Updated Name");
            await Assert.That(result.Description).IsEqualTo("Original Desc");
            await Assert.That(result.SortOrder).IsEqualTo(1);
        }

        [Test]
        public async Task UpdateAlbumAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var request = new UpdateAlbumRequest("Name", null, null, null);

            // Act
            var result = await _service.UpdateAlbumAsync(999, request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task DeleteAlbumAsync_WithExistingAlbum_DeletesAndReturnsTrue()
        {
            // Arrange
            var album = new Album { Name = "Test" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.DeleteAlbumAsync(album.Id);

            // Assert
            await Assert.That(result).IsTrue();

            var deletedAlbum = await _db.Albums.FindAsync(album.Id);
            await Assert.That(deletedAlbum).IsNull();
        }

        [Test]
        public async Task DeleteAlbumAsync_WithNonExistentAlbum_ReturnsFalse()
        {
            // Act
            var result = await _service.DeleteAlbumAsync(999);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task SetCoverPhotoAsync_WithValidIds_SetsCoverPhoto()
        {
            // Arrange
            var photo = new Photo { Filename = "test.jpg", FilePath = "test.jpg", FileSize = 100 };
            var album = new Album { Name = "Test" };
            _db.Photos.Add(photo);
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.SetCoverPhotoAsync(album.Id, photo.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.CoverPhotoId).IsEqualTo(photo.Id);

            var dbAlbum = await _db.Albums.FindAsync(album.Id);
            await Assert.That(dbAlbum!.CoverPhotoId).IsEqualTo(photo.Id);
        }

        [Test]
        public async Task SetCoverPhotoAsync_WithInvalidAlbumId_ReturnsNull()
        {
            // Act
            var result = await _service.SetCoverPhotoAsync(999, 1);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task SetCoverPhotoAsync_WithInvalidPhotoId_ReturnsNull()
        {
            // Arrange
            var album = new Album { Name = "Test" };
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.SetCoverPhotoAsync(album.Id, 999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task RemoveCoverPhotoAsync_RemovesCoverPhoto()
        {
            // Arrange
            var photo = new Photo { Filename = "test.jpg", FilePath = "test.jpg", FileSize = 100 };
            var album = new Album { Name = "Test" };
            _db.Photos.Add(photo);
            _db.Albums.Add(album);
            await _db.SaveChangesAsync();

            album.CoverPhotoId = photo.Id;
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.RemoveCoverPhotoAsync(album.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.CoverPhotoId).IsNull();

            var dbAlbum = await _db.Albums.FindAsync(album.Id);
            await Assert.That(dbAlbum!.CoverPhotoId).IsNull();
        }
    }
}
