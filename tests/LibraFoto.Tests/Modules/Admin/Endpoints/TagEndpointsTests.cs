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
    /// Comprehensive tests for TagEndpoints covering all endpoint methods.
    /// </summary>
    public class TagEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private ITagService _tagService = null!;

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

            _tagService = new TagService(_db);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        #region GetTags Tests

        [Test]
        public async Task GetTags_WithNoTags_ReturnsEmptyList()
        {
            // Act
            var method = typeof(TagEndpoints).GetMethod("GetTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<TagDto>>>)method!.Invoke(null, new object[] { _tagService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(0);
        }

        [Test]
        public async Task GetTags_WithMultipleTags_ReturnsAll()
        {
            // Arrange
            for (int i = 1; i <= 5; i++)
            {
                _db.Tags.Add(new Tag { Id = i, Name = $"Tag {i}" });
            }
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(TagEndpoints).GetMethod("GetTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<IReadOnlyList<TagDto>>>)method!.Invoke(null, new object[] { _tagService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Count).IsEqualTo(5);
        }

        #endregion

        #region GetTagById Tests

        [Test]
        public async Task GetTagById_WithExistingTag_ReturnsTag()
        {
            // Arrange
            _db.Tags.Add(new Tag { Id = 1, Name = "Test Tag" });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(TagEndpoints).GetMethod("GetTagById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, _tagService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Name).IsEqualTo("Test Tag");
        }

        [Test]
        public async Task GetTagById_WithNonExistentTag_ReturnsNotFound()
        {
            // Act
            var method = typeof(TagEndpoints).GetMethod("GetTagById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, _tagService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region CreateTag Tests

        [Test]
        public async Task CreateTag_WithValidData_CreatesTag()
        {
            // Arrange
            var request = new CreateTagRequest("New Tag");

            // Act
            var method = typeof(TagEndpoints).GetMethod("CreateTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Created<TagDto>>)method!.Invoke(null, new object[] { request, _tagService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.Name).IsEqualTo("New Tag");

            var count = await _db.Tags.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        #endregion

        #region UpdateTag Tests

        [Test]
        public async Task UpdateTag_WithValidData_UpdatesTag()
        {
            // Arrange
            _db.Tags.Add(new Tag { Id = 1, Name = "Old Name" });
            await _db.SaveChangesAsync();

            var request = new UpdateTagRequest("New Name");

            // Act
            var method = typeof(TagEndpoints).GetMethod("UpdateTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, request, _tagService, CancellationToken.None })!;

            // Assert
            var okResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>;
            await Assert.That(okResult).IsNotNull();
            await Assert.That(okResult!.Value!.Name).IsEqualTo("New Name");
        }

        [Test]
        public async Task UpdateTag_WithNonExistentTag_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateTagRequest("New Name");

            // Act
            var method = typeof(TagEndpoints).GetMethod("UpdateTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<TagDto>, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, request, _tagService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region DeleteTag Tests

        [Test]
        public async Task DeleteTag_WithExistingTag_DeletesTag()
        {
            // Arrange
            _db.Tags.Add(new Tag { Id = 1, Name = "Test Tag" });
            await _db.SaveChangesAsync();

            // Act
            var method = typeof(TagEndpoints).GetMethod("DeleteTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 1L, _tagService, CancellationToken.None })!;

            // Assert
            var noContentResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NoContent;
            await Assert.That(noContentResult).IsNotNull();

            var count = await _db.Tags.CountAsync();
            await Assert.That(count).IsEqualTo(0);
        }

        [Test]
        public async Task DeleteTag_WithNonExistentTag_ReturnsNotFound()
        {
            // Act
            var method = typeof(TagEndpoints).GetMethod("DeleteTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.NoContent, Microsoft.AspNetCore.Http.HttpResults.NotFound>>)method!.Invoke(null, new object[] { 999L, _tagService, CancellationToken.None })!;

            // Assert
            var notFoundResult = result.Result as Microsoft.AspNetCore.Http.HttpResults.NotFound;
            await Assert.That(notFoundResult).IsNotNull();
        }

        #endregion

        #region AddPhotosToTag Tests

        [Test]
        public async Task AddPhotosToTag_WithValidData_AddsPhotos()
        {
            // Arrange
            _db.Tags.Add(new Tag { Id = 1, Name = "Test Tag" });
            for (int i = 1; i <= 3; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
            }
            await _db.SaveChangesAsync();

            var request = new AddPhotosToTagRequest(new long[] { 1, 2, 3 });

            // Act
            var method = typeof(TagEndpoints).GetMethod("AddPhotosToTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<BulkOperationResult>>)method!.Invoke(null, new object[] { 1L, request, _tagService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.SuccessCount).IsEqualTo(3);

            var count = await _db.PhotoTags.CountAsync();
            await Assert.That(count).IsEqualTo(3);
        }

        #endregion

        #region RemovePhotosFromTag Tests

        [Test]
        public async Task RemovePhotosFromTag_WithValidData_RemovesPhotos()
        {
            // Arrange
            _db.Tags.Add(new Tag { Id = 1, Name = "Test Tag" });
            for (int i = 1; i <= 3; i++)
            {
                _db.Photos.Add(TestHelpers.CreateTestPhoto(id: i, filename: $"photo{i}.jpg"));
                _db.PhotoTags.Add(new PhotoTag { TagId = 1, PhotoId = i });
            }
            await _db.SaveChangesAsync();

            var request = new RemovePhotosFromTagRequest(new long[] { 1, 2 });

            // Act
            var method = typeof(TagEndpoints).GetMethod("RemovePhotosFromTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = await (Task<Microsoft.AspNetCore.Http.HttpResults.Ok<BulkOperationResult>>)method!.Invoke(null, new object[] { 1L, request, _tagService, CancellationToken.None })!;

            // Assert
            await Assert.That(result.Value!.SuccessCount).IsEqualTo(2);

            var count = await _db.PhotoTags.CountAsync();
            await Assert.That(count).IsEqualTo(1);
        }

        #endregion
    }
}
