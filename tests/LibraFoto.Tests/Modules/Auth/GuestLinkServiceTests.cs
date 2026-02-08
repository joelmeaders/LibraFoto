using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.Tests.Modules.Auth;

public class GuestLinkServiceTests
{
    private SqliteConnection _connection = null!;
    private LibraFotoDbContext _db = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _connection = new SqliteConnection($"Data Source=TestDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseSqlite(_connection)
            .EnableDetailedErrors()
            .Options;
        _db = new LibraFotoDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private GuestLinkService CreateService() =>
        new(_db, NullLogger<GuestLinkService>.Instance);

    private async Task<User> CreateTestUser(long id = 1, string email = "test@example.com")
    {
        var user = new User
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Role = UserRole.Admin
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Album> CreateTestAlbum(long id = 1, string name = "Test Album")
    {
        var album = new Album
        {
            Id = id,
            Name = name
        };
        _db.Albums.Add(album);
        await _db.SaveChangesAsync();
        return album;
    }

    private async Task<GuestLink> CreateTestGuestLink(
        string id = "test-link-1",
        string name = "Test Link",
        long createdById = 1,
        DateTime? expiresAt = null,
        int? maxUploads = null,
        int currentUploads = 0,
        long? targetAlbumId = null)
    {
        var link = new GuestLink
        {
            Id = id,
            Name = name,
            CreatedById = createdById,
            DateCreated = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            MaxUploads = maxUploads,
            CurrentUploads = currentUploads,
            TargetAlbumId = targetAlbumId
        };
        _db.GuestLinks.Add(link);
        await _db.SaveChangesAsync();
        return link;
    }

    // 1. CreateGuestLinkAsync - creates link and returns DTO
    [Test]
    public async Task CreateGuestLinkAsync_CreatesLinkAndReturnsDto()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        var request = new CreateGuestLinkRequest("My Guest Link", null, null, null);

        var result = await service.CreateGuestLinkAsync(request, user.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("My Guest Link");
        await Assert.That(result.CreatedByUserId).IsEqualTo(user.Id);
        await Assert.That(result.ExpiresAt).IsNull();
        await Assert.That(result.MaxUploads).IsNull();
        await Assert.That(result.CurrentUploads).IsEqualTo(0);
        await Assert.That(result.TargetAlbumId).IsNull();
        await Assert.That(result.IsActive).IsTrue();
        await Assert.That(result.Id).IsNotNull();
    }

    // 2. CreateGuestLinkAsync - with target album
    [Test]
    public async Task CreateGuestLinkAsync_WithTargetAlbum_SetsAlbumIdAndName()
    {
        var user = await CreateTestUser();
        var album = await CreateTestAlbum(id: 1, name: "Vacation Photos");
        var service = CreateService();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var request = new CreateGuestLinkRequest("Album Link", expiresAt, 10, album.Id);

        var result = await service.CreateGuestLinkAsync(request, user.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Album Link");
        await Assert.That(result.TargetAlbumId).IsEqualTo(album.Id);
        await Assert.That(result.TargetAlbumName).IsEqualTo("Vacation Photos");
        await Assert.That(result.MaxUploads).IsEqualTo(10);
        await Assert.That(result.ExpiresAt).IsNotNull();
    }

    // 3. GetGuestLinksAsync - returns paginated results
    [Test]
    public async Task GetGuestLinksAsync_ReturnsPaginatedResults()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        for (var i = 0; i < 5; i++)
        {
            await CreateTestGuestLink(
                id: $"link-{i}",
                name: $"Link {i}",
                createdById: user.Id,
                expiresAt: DateTime.UtcNow.AddDays(7));
        }

        var result = await service.GetGuestLinksAsync(page: 1, pageSize: 3);

        var links = result.Links.ToList();
        await Assert.That(links.Count).IsEqualTo(3);
        await Assert.That(result.TotalCount).IsEqualTo(5);
    }

    // 4. GetGuestLinksAsync - filters expired links by default
    [Test]
    public async Task GetGuestLinksAsync_FiltersExpiredLinksByDefault()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "active-link",
            name: "Active",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7));
        await CreateTestGuestLink(
            id: "expired-link",
            name: "Expired",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1));
        await CreateTestGuestLink(
            id: "no-expiry-link",
            name: "No Expiry",
            createdById: user.Id,
            expiresAt: null);

        var result = await service.GetGuestLinksAsync(page: 1, pageSize: 10);

        var links = result.Links.ToList();
        await Assert.That(links.Count).IsEqualTo(2);
        await Assert.That(links.Any(l => l.Name == "Active")).IsTrue();
        await Assert.That(links.Any(l => l.Name == "No Expiry")).IsTrue();
        await Assert.That(links.Any(l => l.Name == "Expired")).IsFalse();
    }

    // 5. GetGuestLinksAsync - includes expired links when flag set
    [Test]
    public async Task GetGuestLinksAsync_IncludesExpiredLinksWhenFlagSet()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "active-link",
            name: "Active",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7));
        await CreateTestGuestLink(
            id: "expired-link",
            name: "Expired",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await service.GetGuestLinksAsync(page: 1, pageSize: 10, includeExpired: true);

        var links = result.Links.ToList();
        await Assert.That(links.Count).IsEqualTo(2);
        await Assert.That(links.Any(l => l.Name == "Active")).IsTrue();
        await Assert.That(links.Any(l => l.Name == "Expired")).IsTrue();
    }

    // 6. GetGuestLinkByIdAsync - returns link when found
    [Test]
    public async Task GetGuestLinkByIdAsync_ReturnsLinkWhenFound()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "find-me",
            name: "Findable Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7));

        var result = await service.GetGuestLinkByIdAsync("find-me");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo("find-me");
        await Assert.That(result.Name).IsEqualTo("Findable Link");
        await Assert.That(result.CreatedByUserId).IsEqualTo(user.Id);
    }

    // 7. GetGuestLinkByIdAsync - returns null when not found
    [Test]
    public async Task GetGuestLinkByIdAsync_ReturnsNullWhenNotFound()
    {
        var service = CreateService();

        var result = await service.GetGuestLinkByIdAsync("nonexistent");

        await Assert.That(result).IsNull();
    }

    // 8. GetGuestLinkByCodeAsync - delegates to GetGuestLinkByIdAsync
    [Test]
    public async Task GetGuestLinkByCodeAsync_DelegatesToGetGuestLinkByIdAsync()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "code-link",
            name: "Code Link",
            createdById: user.Id);

        var result = await service.GetGuestLinkByCodeAsync("code-link");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo("code-link");
        await Assert.That(result.Name).IsEqualTo("Code Link");
    }

    // 9. ValidateGuestLinkAsync - valid link returns IsValid true
    [Test]
    public async Task ValidateGuestLinkAsync_ValidLink_ReturnsIsValidTrue()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "valid-link",
            name: "Valid Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7),
            maxUploads: 10,
            currentUploads: 3);

        var result = await service.ValidateGuestLinkAsync("valid-link");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Name).IsEqualTo("Valid Link");
        await Assert.That(result.RemainingUploads).IsEqualTo(7);
    }

    // 10. ValidateGuestLinkAsync - expired link returns IsValid false
    [Test]
    public async Task ValidateGuestLinkAsync_ExpiredLink_ReturnsIsValidFalse()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "expired-link",
            name: "Expired Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await service.ValidateGuestLinkAsync("expired-link");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsFalse();
    }

    // 11. ValidateGuestLinkAsync - exhausted upload limit returns IsValid false
    [Test]
    public async Task ValidateGuestLinkAsync_ExhaustedUploadLimit_ReturnsIsValidFalse()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "exhausted-link",
            name: "Exhausted Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7),
            maxUploads: 5,
            currentUploads: 5);

        var result = await service.ValidateGuestLinkAsync("exhausted-link");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsFalse();
    }

    // 12. ValidateGuestLinkAsync - nonexistent link returns IsValid false
    [Test]
    public async Task ValidateGuestLinkAsync_NonexistentLink_ReturnsIsValidFalse()
    {
        var service = CreateService();

        var result = await service.ValidateGuestLinkAsync("does-not-exist");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsFalse();
    }

    // 13. ValidateGuestLinkAsync - link with no limits is valid
    [Test]
    public async Task ValidateGuestLinkAsync_LinkWithNoLimits_IsValid()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "unlimited-link",
            name: "Unlimited Link",
            createdById: user.Id,
            expiresAt: null,
            maxUploads: null);

        var result = await service.ValidateGuestLinkAsync("unlimited-link");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Name).IsEqualTo("Unlimited Link");
        await Assert.That(result.RemainingUploads).IsNull();
    }

    // 14. RecordUploadAsync - increments upload count
    [Test]
    public async Task RecordUploadAsync_IncrementsUploadCount()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "upload-link",
            name: "Upload Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7),
            maxUploads: 10,
            currentUploads: 2);

        var result = await service.RecordUploadAsync("upload-link");

        await Assert.That(result).IsTrue();
        var link = await _db.GuestLinks.FindAsync("upload-link");
        await Assert.That(link!.CurrentUploads).IsEqualTo(3);
    }

    // 15. RecordUploadAsync - returns false for nonexistent link
    [Test]
    public async Task RecordUploadAsync_ReturnsFalseForNonexistentLink()
    {
        var service = CreateService();

        var result = await service.RecordUploadAsync("nonexistent");

        await Assert.That(result).IsFalse();
    }

    // 16. RecordUploadAsync - returns false for expired link
    [Test]
    public async Task RecordUploadAsync_ReturnsFalseForExpiredLink()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "expired-upload-link",
            name: "Expired Upload Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(-1),
            maxUploads: 10,
            currentUploads: 0);

        var result = await service.RecordUploadAsync("expired-upload-link");

        await Assert.That(result).IsFalse();
    }

    // 17. RecordUploadAsync - returns false when upload limit reached
    [Test]
    public async Task RecordUploadAsync_ReturnsFalseWhenUploadLimitReached()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "maxed-link",
            name: "Maxed Link",
            createdById: user.Id,
            expiresAt: DateTime.UtcNow.AddDays(7),
            maxUploads: 5,
            currentUploads: 5);

        var result = await service.RecordUploadAsync("maxed-link");

        await Assert.That(result).IsFalse();
    }

    // 18. DeleteGuestLinkAsync - deletes existing link
    [Test]
    public async Task DeleteGuestLinkAsync_DeletesExistingLink()
    {
        var user = await CreateTestUser();
        var service = CreateService();
        await CreateTestGuestLink(
            id: "delete-me",
            name: "Delete Me",
            createdById: user.Id);

        var result = await service.DeleteGuestLinkAsync("delete-me");

        await Assert.That(result).IsTrue();
        var link = await _db.GuestLinks.FindAsync("delete-me");
        await Assert.That(link).IsNull();
    }

    // 19. DeleteGuestLinkAsync - returns false for nonexistent link
    [Test]
    public async Task DeleteGuestLinkAsync_ReturnsFalseForNonexistentLink()
    {
        var service = CreateService();

        var result = await service.DeleteGuestLinkAsync("nonexistent");

        await Assert.That(result).IsFalse();
    }

    // 20. GetGuestLinksByUserAsync - returns links for specific user
    [Test]
    public async Task GetGuestLinksByUserAsync_ReturnsLinksForSpecificUser()
    {
        var user1 = await CreateTestUser(id: 1, email: "user1@example.com");
        var user2 = await CreateTestUser(id: 2, email: "user2@example.com");
        var service = CreateService();
        await CreateTestGuestLink(id: "user1-link-1", name: "User1 Link 1", createdById: user1.Id);
        await CreateTestGuestLink(id: "user1-link-2", name: "User1 Link 2", createdById: user1.Id);
        await CreateTestGuestLink(id: "user2-link-1", name: "User2 Link 1", createdById: user2.Id);

        var result = await service.GetGuestLinksByUserAsync(user1.Id);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count()).IsEqualTo(2);
        await Assert.That(result.All(l => l.CreatedByUserId == user1.Id)).IsTrue();
    }
}
