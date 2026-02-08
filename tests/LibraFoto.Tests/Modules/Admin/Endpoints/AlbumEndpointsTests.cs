using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Endpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.Tests.Modules.Admin.Endpoints
{
    /// <summary>
    /// Comprehensive tests for AlbumEndpoints covering all endpoint methods.
    /// </summary>
    public class AlbumEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IAlbumService _albumService = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection)
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .Options;

            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            _albumService = new AlbumService(_db);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        #region GetAlbums Tests

        [Test]
        public async Task GetAlbums_WithNoAlbums_ReturnsEmptyList()
        {
            // Act
            var method = typeof(AlbumEndpoints).GetMethod("GetAlbums", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<AlbumDto>>>)method!.Invoke(null, new object[] { _albumService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task GetAlbums_WithMultipleAlbums_ReturnsAll()
        {
            // Arrange
            for (int i = 1; i <= 5; i++)
            {
                _db.Albums.Add(new Album { Id = i, Name = $"Album {i}", DateCreated = DateTime.UtcNow });
            }
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("GetAlbums", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<AlbumDto>>>)method!.Invoke(null, new object[] { _albumService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(5);
        }

        #endregion

        #region GetAlbumById Tests

        [Test]
        public async Task GetAlbumById_WithExistingAlbum_ReturnsAlbum()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("GetAlbumById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, _albumService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Name).IsEqualTo("Test Album");
        }

        [Test]
        public async Task GetAlbumById_WithNonExistentAlbum_ReturnsNotFound()
        {
            // Act
            var method = typeof(AlbumEndpoints).GetMethod("GetAlbumById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, _albumService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region CreateAlbum Tests

        [Test]
        public async Task CreateAlbum_WithValidData_CreatesAlbum()
        {
            // Arrange
            var request = new CreateAlbumRequest("New Album", "Description");

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("CreateAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Created<AlbumDto>>)method!.Invoke(null, new object[] { request, _albumService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Name).IsEqualTo("New Album");
            await Assert.That(result.Value.Description).IsEqualTo("Description");

            var count = await _db.Albums.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        #endregion

        #region UpdateAlbum Tests

        [Test]
        public async Task UpdateAlbum_WithValidData_UpdatesAlbum()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Old Name", DateCreated = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            var request = new UpdateAlbumRequest("New Name", "New Description");

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("UpdateAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, request, _albumService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Name).IsEqualTo("New Name");
        }

        [Test]
        public async Task UpdateAlbum_WithNonExistentAlbum_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateAlbumRequest("New Name", null);

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("UpdateAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, request, _albumService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region DeleteAlbum Tests

        [Test]
        public async Task DeleteAlbum_WithExistingAlbum_DeletesAlbum()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("DeleteAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, _albumService, CancellationToken.None })!;

            // Assert
            var noContentResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NoContent;
            await Assert.That(noContentResult).IsNotNull();

            var count = await _db.Albums.CountAsync();
            await Assert.That(count).IsEqualTo(0);
        }

        [Test]
        public async Task DeleteAlbum_WithNonExistentAlbum_ReturnsNotFound()
        {
            // Act
            var method = typeof(AlbumEndpoints).GetMethod("DeleteAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, _albumService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region SetCoverPhoto Tests

        [Test]
        public async Task SetCoverPhoto_WithValidData_SetsCoverPhoto()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            _db.Photos.Add(TestHelpers.CreateTestPhoto(id: 1, filename: "cover.jpg"));
            _db.PhotoAlbums.Add(new PhotoAlbum { AlbumId = 1, PhotoId = 1 });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("SetCoverPhoto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, 1L, _albumService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.CoverPhotoId).IsEqualTo(1L);
        }

        [Test]
        public async Task SetCoverPhoto_WithNonExistentAlbum_ReturnsNotFound()
        {
            // Act
            var method = typeof(AlbumEndpoints).GetMethod("SetCoverPhoto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, 1L, _albumService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region RemoveCoverPhoto Tests

        [Test]
        public async Task RemoveCoverPhoto_WithValidData_RemovesCoverPhoto()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", CoverPhotoId = 1, DateCreated = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("RemoveCoverPhoto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, _albumService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<AlbumDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.CoverPhotoId).IsNull();
        }

        #endregion

        #region AddPhotosToAlbum Tests

        [Test]
        public async Task AddPhotosToAlbum_WithValidData_AddsPhotos()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            for (int i = 1; i <= 3; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
            }
            await _db.SaveChangesAsync();

            var request = new AddPhotosToAlbumRequest(new long[] { 1, 2, 3 });

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("AddPhotosToAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<BulkOperationResult>>)method!.Invoke(null, new object[] { 1L, request, _albumService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.SuccessCount).IsEqualTo(3);

            var count = await _db.PhotoAlbums.CountAsync();
            await Assert.That(count).IsEqualTo(3);
        }

        #endregion

        #region RemovePhotosFromAlbum Tests

        [Test]
        public async Task RemovePhotosFromAlbum_WithValidData_RemovesPhotos()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            for (int i = 1; i <= 3; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
                _db.PhotoAlbums.Add(new PhotoAlbum { AlbumId = 1, PhotoId = i });
            }
            await _db.SaveChangesAsync();

            var request = new RemovePhotosFromAlbumRequest(new long[] { 1, 2 });

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("RemovePhotosFromAlbum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<BulkOperationResult>>)method!.Invoke(null, new object[] { 1L, request, _albumService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);

            var count = await _db.PhotoAlbums.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        #endregion

        #region ReorderPhotos Tests

        [Test]
        public async Task ReorderPhotos_WithValidData_ReordersPhotos()
        {
            // Arrange
            _db.Albums.Add(new Album { Id = 1, Name = "Test Album", DateCreated = DateTime.UtcNow });
            for (int i = 1; i <= 3; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
                _db.PhotoAlbums.Add(new PhotoAlbum { AlbumId = 1, PhotoId = i, SortOrder = i });
            }
            await _db.SaveChangesAsync();

            var request = new ReorderPhotosRequest(new[]
            {
                new PhotoOrder(1, 3),
                new PhotoOrder(2, 2),
                new PhotoOrder(3, 1)
            });

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("ReorderPhotos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, request, _albumService, CancellationToken.None })!;

            // Assert
            var noContentResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NoContent;
            await Assert.That(noContentResult).IsNotNull();

            var photoAlbum1 = await _db.PhotoAlbums.FindAsync(1L, 1L);
            await Assert.That(photoAlbum1!.SortOrder).IsEqualTo(3);
        }

        [Test]
        public async Task ReorderPhotos_WithNonExistentAlbum_ReturnsNotFound()
        {
            // Arrange
            var request = new ReorderPhotosRequest(new[] { new PhotoOrder(1, 1) });

            // Act
            var method = typeof(AlbumEndpoints).GetMethod("ReorderPhotos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, request, _albumService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion
    }
}
