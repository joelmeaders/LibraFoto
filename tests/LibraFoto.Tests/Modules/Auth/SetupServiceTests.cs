using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace LibraFoto.Tests.Modules.Auth
{
    public class SetupServiceTests
    {
        private IUserService _userService = null!;
        private IAuthService _authService = null!;
        private SetupService _setupService = null!;

        [Before(Test)]
        public void Setup()
        {
            _userService = Substitute.For<IUserService>();
            _authService = Substitute.For<IAuthService>();
            _setupService = new SetupService(
                _userService,
                _authService,
                NullLogger<SetupService>.Instance);
        }

        [Test]
        public async Task IsSetupRequiredAsync_ReturnsTrue_WhenNoUsersExist()
        {
            // Arrange
            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(0);

            // Act
            var result = await _setupService.IsSetupRequiredAsync(CancellationToken.None);

            // Assert
            await Assert.That(result).IsTrue();
        }

        [Test]
        public async Task IsSetupRequiredAsync_ReturnsFalse_WhenUsersExist()
        {
            // Arrange
            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(3);

            // Act
            var result = await _setupService.IsSetupRequiredAsync(CancellationToken.None);

            // Assert
            await Assert.That(result).IsFalse();
        }

        [Test]
        public async Task CompleteSetupAsync_CreatesAdminAndReturnsLoginResponse_WhenNoUsersExist()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "SecurePass123");
            var adminUser = new UserDto(1, "admin@example.com", UserRole.Admin, DateTime.UtcNow, null);
            var loginResponse = new LoginResponse(
                "jwt-token",
                "refresh-token",
                DateTime.UtcNow.AddHours(1),
                adminUser);

            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(0);
            _userService.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
                .Returns(adminUser);
            _authService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
                .Returns(loginResponse);

            // Act
            var result = await _setupService.CompleteSetupAsync(request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Token).IsEqualTo("jwt-token");
            await Assert.That(result.RefreshToken).IsEqualTo("refresh-token");
            await Assert.That(result.User.Email).IsEqualTo("admin@example.com");
            await Assert.That(result.User.Role).IsEqualTo(UserRole.Admin);

            await _userService.Received(1).CreateUserAsync(
                Arg.Is<CreateUserRequest>(r =>
                    r.Email == "admin@example.com" &&
                    r.Password == "SecurePass123" &&
                    r.Role == UserRole.Admin),
                Arg.Any<CancellationToken>());

            await _authService.Received(1).LoginAsync(
                Arg.Is<LoginRequest>(r =>
                    r.Email == "admin@example.com" &&
                    r.Password == "SecurePass123"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CompleteSetupAsync_ReturnsNull_WhenUsersAlreadyExist()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "SecurePass123");

            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(1);

            // Act
            var result = await _setupService.CompleteSetupAsync(request, CancellationToken.None);

            // Assert
            await Assert.That(result).IsNull();

            await _userService.DidNotReceive().CreateUserAsync(
                Arg.Any<CreateUserRequest>(),
                Arg.Any<CancellationToken>());

            await _authService.DidNotReceive().LoginAsync(
                Arg.Any<LoginRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetSetupStatusAsync_ReturnsIsRequiredTrue_WhenNoUsersExist()
        {
            // Arrange
            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(0);

            // Act
            var result = await _setupService.GetSetupStatusAsync(CancellationToken.None);

            // Assert
            await Assert.That(result.IsSetupRequired).IsTrue();
            await Assert.That(result.Message).IsEqualTo("Initial setup required. Please create the first admin user.");
        }

        [Test]
        public async Task GetSetupStatusAsync_ReturnsIsRequiredFalse_WhenUsersExist()
        {
            // Arrange
            _userService.GetUserCountAsync(Arg.Any<CancellationToken>())
                .Returns(2);

            // Act
            var result = await _setupService.GetSetupStatusAsync(CancellationToken.None);

            // Assert
            await Assert.That(result.IsSetupRequired).IsFalse();
            await Assert.That(result.Message).IsEqualTo("Setup has been completed.");
        }
    }
}
