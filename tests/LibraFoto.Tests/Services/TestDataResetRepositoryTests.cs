using BCrypt.Net;
using LibraFoto.Api.Repositories;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Tests.Services;

public class TestDataResetRepositoryTests
{
    private SqliteConnection _connection = null!;
    private LibraFotoDbContext _db = null!;
    private TestDataResetRepository _repository = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _connection = await TestDbContextFactory.CreateOpenConnectionAsync();
        _db = new LibraFotoDbContext(TestDbContextFactory.CreateInMemoryOptions(_connection));
        await _db.Database.EnsureCreatedAsync();
        _repository = new TestDataResetRepository(_db);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task ResetDatabaseAsync_RemovesExistingData_AndSeedsDefaults()
    {
        // Arrange
        await SeedExistingDataAsync();
        const string StoragePath = @"C:\temp\librafoto\photos";
        const string AdminEmail = "admin@example.com";
        const string AdminPassword = "test-password-123";

        // Act
        await _repository.ResetDatabaseAsync(StoragePath, AdminEmail, AdminPassword);

        // Assert - cleared tables
        await Assert.That(await _db.PhotoAlbums.CountAsync()).IsEqualTo(0);
        await Assert.That(await _db.PhotoTags.CountAsync()).IsEqualTo(0);
        await Assert.That(await _db.GuestLinks.CountAsync()).IsEqualTo(0);
        await Assert.That(await _db.Photos.CountAsync()).IsEqualTo(0);
        await Assert.That(await _db.Albums.CountAsync()).IsEqualTo(0);
        await Assert.That(await _db.Tags.CountAsync()).IsEqualTo(0);

        // Assert - default display settings
        var settings = await _db.DisplaySettings.SingleAsync();
        await Assert.That(settings.Name).IsEqualTo("Default");
        await Assert.That(settings.SlideDuration).IsEqualTo(10);
        await Assert.That(settings.Transition).IsEqualTo(TransitionType.Fade);
        await Assert.That(settings.TransitionDuration).IsEqualTo(1000);
        await Assert.That(settings.Shuffle).IsFalse();
        await Assert.That(settings.SourceType).IsEqualTo(SourceType.All);
        await Assert.That(settings.IsActive).IsTrue();

        // Assert - default local storage provider
        var provider = await _db.StorageProviders.SingleAsync();
        await Assert.That(provider.Name).IsEqualTo("Local Storage");
        await Assert.That(provider.Type).IsEqualTo(StorageProviderType.Local);
        await Assert.That(provider.IsEnabled).IsTrue();
        await Assert.That(provider.Configuration).Contains(StoragePath.Replace("\\", "\\\\"));

        // Assert - test admin user
        var admin = await _db.Users.SingleAsync();
        await Assert.That(admin.Email).IsEqualTo(AdminEmail);
        await Assert.That(admin.Role).IsEqualTo(UserRole.Admin);
        await Assert.That(BCrypt.Net.BCrypt.Verify(AdminPassword, admin.PasswordHash)).IsTrue();
    }

    [Test]
    public async Task ResetDatabaseAsync_WhenDatabaseIsEmpty_CreatesRequiredDefaultData()
    {
        // Arrange
        const string StoragePath = @"D:\data\photos";
        const string AdminEmail = "fresh-admin@example.com";
        const string AdminPassword = "fresh-password";

        // Act
        await _repository.ResetDatabaseAsync(StoragePath, AdminEmail, AdminPassword);

        // Assert
        await Assert.That(await _db.DisplaySettings.CountAsync()).IsEqualTo(1);
        await Assert.That(await _db.StorageProviders.CountAsync()).IsEqualTo(1);
        await Assert.That(await _db.Users.CountAsync()).IsEqualTo(1);
    }

    private async Task SeedExistingDataAsync()
    {
        var user = new User
        {
            Email = "old-admin@example.com",
            PasswordHash = "old-hash",
            Role = UserRole.Admin
        };

        var album = new Album { Name = "Old Album" };
        var tag = new Tag { Name = "old-tag" };
        var photo = new Photo
        {
            Filename = "old-photo.jpg",
            OriginalFilename = "old-photo-original.jpg",
            FilePath = "old/old-photo.jpg",
            Width = 100,
            Height = 100,
            FileSize = 500
        };

        _db.Users.Add(user);
        _db.Albums.Add(album);
        _db.Tags.Add(tag);
        _db.Photos.Add(photo);
        _db.DisplaySettings.Add(new DisplaySettings
        {
            Name = "Old Settings",
            IsActive = false,
            Shuffle = true,
            SourceType = SourceType.Tag,
            SourceId = 123
        });
        _db.StorageProviders.Add(new StorageProvider
        {
            Name = "Old Provider",
            Type = StorageProviderType.GooglePhotos,
            IsEnabled = false
        });

        await _db.SaveChangesAsync();

        _db.PhotoAlbums.Add(new PhotoAlbum { PhotoId = photo.Id, AlbumId = album.Id });
        _db.PhotoTags.Add(new PhotoTag { PhotoId = photo.Id, TagId = tag.Id });
        _db.GuestLinks.Add(new GuestLink
        {
            Id = "oldguest001",
            Name = "Old Guest Link",
            CreatedById = user.Id,
            DateCreated = DateTime.UtcNow.AddDays(-3)
        });

        await _db.SaveChangesAsync();
    }
}