using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraFoto.Tests.Modules.Auth
{
    /// <summary>
    /// Tests for UserService using in-memory SQLite database.
    /// </summary>
    public class UserServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private UserService _service = null!;

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

            _service = new UserService(_db, NullLogger<UserService>.Instance);
        }

        [After(Test)]
        public async Task Cleanup()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetUserCountAsync_WithNoUsers_ReturnsZero()
        {
            // Act
            var count = await _service.GetUserCountAsync();

            // Assert
            await Assert.That(count).IsZero();
        }

        [Test]
        public async Task GetUserCountAsync_WithUsers_ReturnsCorrectCount()
        {
            // Arrange
            _db.Users.AddRange(
                new User { Email = "user1@test.com", PasswordHash = "hash1", Role = UserRole.Admin },
                new User { Email = "user2@test.com", PasswordHash = "hash2", Role = UserRole.Editor }
            );
            await _db.SaveChangesAsync();

            // Act
            var count = await _service.GetUserCountAsync();

            // Assert
            await Assert.That(count).IsEqualTo(2);
        }

        [Test]
        public async Task CreateUserAsync_WithValidData_CreatesUser()
        {
            // Arrange
            var request = new CreateUserRequest("test@example.com", "password123", UserRole.Editor);

            // Act
            var result = await _service.CreateUserAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.Email).IsEqualTo("test@example.com");
            await Assert.That(result.Role).IsEqualTo(UserRole.Editor);

            var dbUser = await _db.Users.FirstAsync();
            await Assert.That(dbUser.Email).IsEqualTo("test@example.com");
            await Assert.That(BCrypt.Net.BCrypt.Verify("password123", dbUser.PasswordHash)).IsTrue();
        }

        [Test]
        public async Task CreateUserAsync_WithDuplicateEmail_ThrowsException()
        {
            // Arrange
            _db.Users.Add(new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor });
            await _db.SaveChangesAsync();

            var request = new CreateUserRequest("test@example.com", "password", UserRole.Editor);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateUserAsync(request));
        }

        [Test]
        public async Task CreateUserAsync_WithDifferentCase_ThrowsException()
        {
            // Arrange
            _db.Users.Add(new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor });
            await _db.SaveChangesAsync();

            var request = new CreateUserRequest("TEST@EXAMPLE.COM", "password", UserRole.Editor);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateUserAsync(request));
        }

        [Test]
        public async Task GetUserByIdAsync_WithExistingId_ReturnsUser()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Admin };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetUserByIdAsync(user.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Email).IsEqualTo("test@example.com");
            await Assert.That(result.Role).IsEqualTo(UserRole.Admin);
        }

        [Test]
        public async Task GetUserByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Act
            var result = await _service.GetUserByIdAsync(999);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task GetUserByEmailAsync_WithExistingEmail_ReturnsUser()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetUserByEmailAsync("test@example.com");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task GetUserByEmailAsync_IsCaseInsensitive()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetUserByEmailAsync("TEST@EXAMPLE.COM");

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task GetUserByEmailAsync_WithNonExistentEmail_ReturnsNull()
        {
            // Act
            var result = await _service.GetUserByEmailAsync("nonexistent@example.com");

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdateUserAsync_WithNewEmail_UpdatesEmail()
        {
            // Arrange
            var user = new User { Email = "old@example.com", PasswordHash = "hash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new UpdateUserRequest(Email: "new@example.com", Password: null, Role: null);

            // Act
            var result = await _service.UpdateUserAsync(user.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Email).IsEqualTo("new@example.com");

            var dbUser = await _db.Users.FindAsync(user.Id);
            await Assert.That(dbUser!.Email).IsEqualTo("new@example.com");
        }

        [Test]
        public async Task UpdateUserAsync_WithNewPassword_UpdatesPasswordHash()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "oldhash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new UpdateUserRequest(Email: null, Password: "newpassword", Role: null);

            // Act
            var result = await _service.UpdateUserAsync(user.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();

            var dbUser = await _db.Users.FindAsync(user.Id);
            await Assert.That(BCrypt.Net.BCrypt.Verify("newpassword", dbUser!.PasswordHash)).IsTrue();
        }

        [Test]
        public async Task UpdateUserAsync_WithNewRole_UpdatesRole()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new UpdateUserRequest(Email: null, Password: null, Role: UserRole.Admin);

            // Act
            var result = await _service.UpdateUserAsync(user.Id, request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Role).IsEqualTo(UserRole.Admin);

            var dbUser = await _db.Users.FindAsync(user.Id);
            await Assert.That(dbUser!.Role).IsEqualTo(UserRole.Admin);
        }

        [Test]
        public async Task UpdateUserAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var request = new UpdateUserRequest(Email: "test@example.com", Password: null, Role: null);

            // Act
            var result = await _service.UpdateUserAsync(999, request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task UpdateUserAsync_WithDuplicateEmail_ThrowsException()
        {
            // Arrange
            _db.Users.AddRange(
                new User { Email = "user1@example.com", PasswordHash = "hash1", Role = UserRole.Editor },
                new User { Email = "user2@example.com", PasswordHash = "hash2", Role = UserRole.Editor }
            );
            await _db.SaveChangesAsync();

            var user1 = await _db.Users.FirstAsync(u => u.Email == "user1@example.com");
            var request = new UpdateUserRequest(Email: "user2@example.com", Password: null, Role: null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.UpdateUserAsync(user1.Id, request));
        }

        [Test]
        public async Task DeleteUserAsync_WithExistingUser_DeletesAndReturnsTrue()
        {
            // Arrange
            var user = new User { Email = "test@example.com", PasswordHash = "hash", Role = UserRole.Editor };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.DeleteUserAsync(user.Id);

            // Assert
            await Assert.That(result).IsTrue();

            var deletedUser = await _db.Users.FindAsync(user.Id);
            await Assert.That(deletedUser).IsNull();
        }

        [Test]
        public async Task DeleteUserAsync_WithNonExistentUser_ReturnsFalse()
        {
            // Act
            var result = await _service.DeleteUserAsync(999);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
        {
            // Arrange
            for (int i = 1; i <= 25; i++)
            {
                _db.Users.Add(new User { Email = $"user{i}@example.com", PasswordHash = "hash", Role = UserRole.Editor });
            }
            await _db.SaveChangesAsync();

            // Act
            var (users, totalCount) = await _service.GetUsersAsync(page: 2, pageSize: 10);

            // Assert
            await Assert.That(totalCount).IsEqualTo(25);
            await Assert.That(users.Count()).IsEqualTo(10);
        }

        [Test]
        public async Task GetUsersAsync_OrdersByEmail()
        {
            // Arrange
            _db.Users.AddRange(
                new User { Email = "c@example.com", PasswordHash = "hash", Role = UserRole.Editor },
                new User { Email = "a@example.com", PasswordHash = "hash", Role = UserRole.Editor },
                new User { Email = "b@example.com", PasswordHash = "hash", Role = UserRole.Editor }
            );
            await _db.SaveChangesAsync();

            // Act
            var (users, _) = await _service.GetUsersAsync();

            // Assert
            var emails = users.Select(u => u.Email).ToArray();
            await Assert.That(emails[0]).IsEqualTo("a@example.com");
            await Assert.That(emails[1]).IsEqualTo("b@example.com");
            await Assert.That(emails[2]).IsEqualTo("c@example.com");
        }
    }
}
