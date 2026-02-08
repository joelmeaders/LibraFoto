using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Auth
{
    /// <summary>
    /// Tests for AuthService JWT authentication logic.
    /// </summary>
    public class AuthServiceTests
    {
        private SqliteConnection _connection = null!;
        private LibraFotoDbContext _db = null!;
        private ServiceProvider _serviceProvider = null!;
        private AuthService _service = null!;
        private IConfiguration _configuration = null!;

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

            // Setup configuration for JWT
            var configData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "LibraFoto-Test-Secret-Key-At-Least-32-Characters-Long",
                ["Jwt:Issuer"] = "LibraFotoTest",
                ["Jwt:Audience"] = "LibraFotoTest",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Setup DI container for testing singleton service
            var services = new ServiceCollection();
            services.AddScoped<LibraFotoDbContext>(_ =>
            {
                var opts = new DbContextOptionsBuilder<LibraFotoDbContext>()
                    .UseSqlite(_connection).Options;
                return new LibraFotoDbContext(opts);
            });
            services.AddScoped<IUserService, UserService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IConfiguration>(_configuration);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            _serviceProvider = services.BuildServiceProvider();
            _service = (AuthService)_serviceProvider.GetRequiredService<IAuthService>();
        }

        [After(Test)]
        public async Task Cleanup()
        {
            // Clear static state to prevent test interference
            AuthService.ClearStaticState();
            _serviceProvider.Dispose();
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("test@example.com", "password123");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Token).IsNotEmpty();
            await Assert.That(result.RefreshToken).IsNotEmpty();
            await Assert.That(result.User.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task LoginAsync_IsCaseInsensitive()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("TEST@EXAMPLE.COM", "password123");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.User.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task LoginAsync_WithInvalidEmail_ReturnsNull()
        {
            // Arrange
            var request = new LoginRequest("nonexistent@example.com", "password");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("test@example.com", "wrongpassword");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task LoginAsync_UpdatesLastLoginTime()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            var userId = user.Id;

            var request = new LoginRequest("test@example.com", "password123");

            // Act
            await _service.LoginAsync(request);

            // Assert - Create fresh scope to query updated data
            using var scope = _serviceProvider.CreateScope();
            var freshDb = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            var updatedUser = await freshDb.Users.FirstAsync(u => u.Id == userId);
            await Assert.That(updatedUser.LastLogin).IsNotNull();
        }

        [Test]
        public async Task ValidateTokenAsync_WithValidToken_ReturnsUserId()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("test@example.com", "password123");
            var loginResponse = await _service.LoginAsync(request);

            // Act
            var userId = await _service.ValidateTokenAsync(loginResponse!.Token);

            // Assert
            await Assert.That(userId).IsNotNull();
            await Assert.That(userId!.Value).IsEqualTo(user.Id);
        }

        [Test]
        public async Task ValidateTokenAsync_WithInvalidToken_ReturnsNull()
        {
            // Act  
            var userId = await _service.ValidateTokenAsync("invalid.token.here");

            // Assert
            await Assert.That(userId).IsNull();
        }

        [Test]
        public async Task RefreshTokenAsync_WithValidRefreshToken_ReturnsNewTokens()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("test@example.com", "password123");
            var loginResponse = await _service.LoginAsync(request);

            // Act
            var refreshResponse = await _service.RefreshTokenAsync(loginResponse!.RefreshToken);

            // Assert
            await Assert.That(refreshResponse).IsNotNull();
            await Assert.That(refreshResponse!.Token).IsNotEmpty();
            await Assert.That(refreshResponse.RefreshToken).IsNotEmpty();
        }

        [Test]
        public async Task RefreshTokenAsync_WithInvalidRefreshToken_ReturnsNull()
        {
            // Act
            var result = await _service.RefreshTokenAsync("invalid-refresh-token");

            // Assert
            await Assert.That(result).IsNull();
        }

        [Test]
        public async Task LogoutAsync_RemovesRefreshTokens()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Editor
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new LoginRequest("test@example.com", "password123");
            var loginResponse = await _service.LoginAsync(request);

            // Act
            await _service.LogoutAsync(user.Id);

            // After logout, refresh token should be invalid
            var refreshResponse = await _service.RefreshTokenAsync(loginResponse!.RefreshToken);

            // Assert
            await Assert.That(refreshResponse).IsNull();
        }

        [Test]
        public async Task GetCurrentUserAsync_ReturnsUserDto()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Admin
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Act
            var result = await _service.GetCurrentUserAsync(user.Id);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Email).IsEqualTo("test@example.com");
            await Assert.That(result.Role).IsEqualTo(UserRole.Admin);
        }
    }
}
