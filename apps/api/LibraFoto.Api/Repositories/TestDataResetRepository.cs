using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Api.Repositories;

/// <summary>
/// Entity Framework implementation of test data reset repository.
/// </summary>
public sealed class TestDataResetRepository : ITestDataResetRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public TestDataResetRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ResetDatabaseAsync(
        string storagePath,
        string testAdminEmail,
        string testAdminPassword,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        await _dbContext.PhotoAlbums.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PhotoTags.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.GuestLinks.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Photos.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Albums.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Tags.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Users.ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DisplaySettings.ExecuteDeleteAsync(cancellationToken);

        var defaultSettings = new DisplaySettings
        {
            Name = "Default",
            SlideDuration = 10,
            Transition = TransitionType.Fade,
            TransitionDuration = 1000,
            Shuffle = false,
            SourceType = SourceType.All,
            SourceId = null,
            IsActive = true
        };
        _dbContext.DisplaySettings.Add(defaultSettings);

        await _dbContext.StorageProviders.ExecuteDeleteAsync(cancellationToken);

        var localProvider = new StorageProvider
        {
            Name = "Local Storage",
            Type = StorageProviderType.Local,
            IsEnabled = true,
            Configuration = $"{{\"basePath\": \"{storagePath.Replace("\\", "\\\\")}\"}}",
            LastSyncDate = null
        };
        _dbContext.StorageProviders.Add(localProvider);

        var testAdmin = new User
        {
            Email = testAdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(testAdminPassword),
            Role = UserRole.Admin,
            DateCreated = DateTime.UtcNow
        };
        _dbContext.Users.Add(testAdmin);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
