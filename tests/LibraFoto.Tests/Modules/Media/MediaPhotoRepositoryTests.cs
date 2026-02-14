using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Media.Services.Repositories;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Tests.Modules.Media;

public class MediaPhotoRepositoryTests
{
    private SqliteConnection _connection = null!;
    private LibraFotoDbContext _db = null!;
    private MediaPhotoRepository _repository = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _connection = await TestDbContextFactory.CreateOpenConnectionAsync();
        _db = new LibraFotoDbContext(TestDbContextFactory.CreateInMemoryOptions(_connection));
        await _db.Database.EnsureCreatedAsync();
        _repository = new MediaPhotoRepository(_db);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task GetPhotoByIdAsync_ReturnsPhoto_WhenPhotoExists()
    {
        // Arrange
        var photo = CreatePhoto();
        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        // Act
        var result = await _repository.GetPhotoByIdAsync(photo.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(photo.Id);
        await Assert.That(result.Filename).IsEqualTo(photo.Filename);
    }

    [Test]
    public async Task GetPhotoByIdAsync_ReturnsNull_WhenPhotoDoesNotExist()
    {
        // Act
        var result = await _repository.GetPhotoByIdAsync(999_999);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SaveChangesAsync_PersistsNewPhoto()
    {
        // Arrange
        var photo = CreatePhoto();
        _db.Photos.Add(photo);

        // Act
        await _repository.SaveChangesAsync();

        // Assert
        var persisted = await _db.Photos.SingleAsync();
        await Assert.That(persisted.OriginalFilename).IsEqualTo(photo.OriginalFilename);
        await Assert.That(persisted.FilePath).IsEqualTo(photo.FilePath);
    }

    [Test]
    public async Task SaveChangesAsync_PersistsUpdatedPhoto()
    {
        // Arrange
        var photo = CreatePhoto();
        _db.Photos.Add(photo);
        await _db.SaveChangesAsync();

        photo.Location = "Paris, France";

        // Act
        await _repository.SaveChangesAsync();

        // Assert
        var updated = await _db.Photos
            .AsNoTracking()
            .SingleAsync(p => p.Id == photo.Id);

        await Assert.That(updated.Location).IsEqualTo("Paris, France");
    }

    private static Photo CreatePhoto() => new()
    {
        Filename = "test-photo.jpg",
        OriginalFilename = "original-test-photo.jpg",
        FilePath = "photos/test-photo.jpg",
        Width = 1920,
        Height = 1080,
        FileSize = 2048
    };
}