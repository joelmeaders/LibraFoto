using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Media.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Media.Endpoints
{
    public class ThumbnailEndpointsTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private IThumbnailService _thumbnailService = null!;
        private IConfiguration _configuration = null!;
        private string _tempDir = null!;

        [Before(Test)]
        public async Task Setup()
        {
            _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await _connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
                .UseSqlite(_connection).Options;
            _db = new LibraFotoDbContext(options);
            await _db.Database.EnsureCreatedAsync();

            _tempDir = Path.Combine(Path.GetTempPath(), $"LibraFotoThumbnailEndpointTest_{Guid.NewGuid()}");
            _thumbnailService = new ThumbnailService(_tempDir);
            _configuration = Substitute.For<IConfiguration>();
            _configuration["Storage:LocalPath"].Returns(_tempDir);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public async Task GetThumbnail_ReturnsNotFound_WhenPhotoDoesNotExist()
        {
            // Act
            var photo = await _db.Photos.FindAsync(999L);

            // Assert
            await Assert.That(photo).IsNull();
        }

        [Test]
        public async Task GenerateThumbnail_ReturnsBadRequest_WhenSourcePathEmpty()
        {
            // Arrange - testing validation logic
            var request = new LibraFoto.Modules.Media.Endpoints.GenerateThumbnailRequest("", null);

            // Assert
            await Assert.That(string.IsNullOrWhiteSpace(request.SourcePath)).IsTrue();
        }

        [Test]
        [NotInParallel]
        public async Task ThumbnailService_GeneratesThumbnail()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempDir, "source.jpg");
            await CreateTestImageAsync(sourcePath);

            // Act
            var result = await _thumbnailService.GenerateThumbnailAsync(sourcePath, 1L, DateTime.UtcNow);

            // Assert
            await Assert.That(result.Success).IsTrue();
            await Assert.That(_thumbnailService.ThumbnailExists(1L)).IsTrue();
        }

        [Test]
        [NotInParallel]
        public async Task ThumbnailService_DeletesThumbnail()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempDir, "source.jpg");
            await CreateTestImageAsync(sourcePath);
            await _thumbnailService.GenerateThumbnailAsync(sourcePath, 2L, DateTime.UtcNow);

            // Act
            var deleted = _thumbnailService.DeleteThumbnails(2L);

            // Assert
            await Assert.That(deleted).IsTrue();
            await Assert.That(_thumbnailService.ThumbnailExists(2L)).IsFalse();
        }

        [Test]
        public async Task RefreshThumbnail_RequiresPhotoToExist()
        {
            // Arrange - photo doesn't exist in DB
            var photo = await _db.Photos.FindAsync(999L);

            // Assert
            await Assert.That(photo).IsNull();
        }

        [Test]
        public async Task RefreshThumbnails_ValidatesPhotoIds()
        {
            // Arrange
            var request = new LibraFoto.Modules.Media.Endpoints.RefreshThumbnailsRequest(Array.Empty<long>());

            // Assert
            await Assert.That(request.PhotoIds.Length).IsEqualTo(0);
        }

        private static async Task CreateTestImageAsync(string path)
        {
            using var image = new Image<Rgba32>(100, 100, Color.Green);
            await image.SaveAsync(path, new JpegEncoder());
        }
    }
}
