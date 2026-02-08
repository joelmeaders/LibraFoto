using System.Security.Claims;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Endpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Auth.Endpoints
{
    /// <summary>
    /// Tests for AuthEndpoints - authentication endpoints.
    /// </summary>
    public class AuthEndpointsTests
    {
        private IAuthService _authService = null!;

        [Before(Test)]
        public void Setup()
        {
            _authService = Substitute.For<IAuthService>();
        }

        #region Login Tests

        [Test]
        public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
        {
            // Arrange
            var request = new LoginRequest("test@example.com", "password123");
            var expectedResponse = new LoginResponse(
                "token123",
                "refreshToken123",
                DateTime.UtcNow.AddHours(1),
                new UserDto(1, "test@example.com", UserRole.Editor, DateTime.UtcNow, null)
            );
            _authService.LoginAsync(request, Arg.Any<CancellationToken>())
                .Returns(expectedResponse);

            // Act
            var result = await AuthEndpoints_TestHelper.Login(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<LoginResponse>>();
            var okResult = (Ok<LoginResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Token).IsEqualTo("token123");
            await Assert.That(okResult.Value.RefreshToken).IsEqualTo("refreshToken123");
            await Assert.That(okResult.Value.User.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest("test@example.com", "wrongpassword");
            _authService.LoginAsync(request, Arg.Any<CancellationToken>())
                .Returns((LoginResponse?)null);

            // Act
            var result = await AuthEndpoints_TestHelper.Login(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        [Test]
        public async Task Login_WithEmptyEmail_ReturnsValidationProblem()
        {
            // Arrange
            var request = new LoginRequest("", "password123");

            // Act
            var result = await AuthEndpoints_TestHelper.Login(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("credentials");
        }

        [Test]
        public async Task Login_WithEmptyPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new LoginRequest("test@example.com", "");

            // Act
            var result = await AuthEndpoints_TestHelper.Login(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("credentials");
        }

        [Test]
        public async Task Login_WithWhitespaceCredentials_ReturnsValidationProblem()
        {
            // Arrange
            var request = new LoginRequest("   ", "   ");

            // Act
            var result = await AuthEndpoints_TestHelper.Login(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        }

        #endregion

        #region Logout Tests

        [Test]
        public async Task Logout_WithValidUser_ReturnsOk()
        {
            // Arrange
            var user = CreateClaimsPrincipal(123);
            _authService.LogoutAsync(123, Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var result = await AuthEndpoints_TestHelper.Logout(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok>();
            await _authService.Received(1).LogoutAsync(123, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task Logout_WithNoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = await AuthEndpoints_TestHelper.Logout(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        [Test]
        public async Task Logout_WithInvalidUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "not-a-number")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await AuthEndpoints_TestHelper.Logout(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        #endregion

        #region GetCurrentUser Tests

        [Test]
        public async Task GetCurrentUser_WithValidUser_ReturnsOkWithUserDto()
        {
            // Arrange
            var user = CreateClaimsPrincipal(456);
            var expectedUser = new UserDto(456, "test@example.com", UserRole.Admin, DateTime.UtcNow, null);
            _authService.GetCurrentUserAsync(456, Arg.Any<CancellationToken>())
                .Returns(expectedUser);

            // Act
            var result = await AuthEndpoints_TestHelper.GetCurrentUser(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UserDto>>();
            var okResult = (Ok<UserDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(456);
            await Assert.That(okResult.Value.Email).IsEqualTo("test@example.com");
        }

        [Test]
        public async Task GetCurrentUser_WhenUserNotFound_ReturnsNotFound()
        {
            // Arrange
            var user = CreateClaimsPrincipal(999);
            _authService.GetCurrentUserAsync(999, Arg.Any<CancellationToken>())
                .Returns((UserDto?)null);

            // Act
            var result = await AuthEndpoints_TestHelper.GetCurrentUser(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound>();
        }

        [Test]
        public async Task GetCurrentUser_WithNoUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = await AuthEndpoints_TestHelper.GetCurrentUser(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        [Test]
        public async Task GetCurrentUser_WithInvalidUserIdClaim_ReturnsUnauthorized()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "invalid")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

            // Act
            var result = await AuthEndpoints_TestHelper.GetCurrentUser(user, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        #endregion

        #region RefreshToken Tests

        [Test]
        public async Task RefreshToken_WithValidToken_ReturnsOkWithNewTokens()
        {
            // Arrange
            var request = new RefreshTokenRequest("valid-refresh-token");
            var expectedResponse = new LoginResponse(
                "new-token",
                "new-refresh-token",
                DateTime.UtcNow.AddHours(1),
                new UserDto(1, "test@example.com", UserRole.Editor, DateTime.UtcNow, null)
            );
            _authService.RefreshTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
                .Returns(expectedResponse);

            // Act
            var result = await AuthEndpoints_TestHelper.RefreshToken(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<LoginResponse>>();
            var okResult = (Ok<LoginResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Token).IsEqualTo("new-token");
        }

        [Test]
        public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var request = new RefreshTokenRequest("invalid-refresh-token");
            _authService.RefreshTokenAsync("invalid-refresh-token", Arg.Any<CancellationToken>())
                .Returns((LoginResponse?)null);

            // Act
            var result = await AuthEndpoints_TestHelper.RefreshToken(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<UnauthorizedHttpResult>();
        }

        [Test]
        public async Task RefreshToken_WithEmptyToken_ReturnsValidationProblem()
        {
            // Arrange
            var request = new RefreshTokenRequest("");

            // Act
            var result = await AuthEndpoints_TestHelper.RefreshToken(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("refreshToken");
        }

        [Test]
        public async Task RefreshToken_WithWhitespaceToken_ReturnsValidationProblem()
        {
            // Arrange
            var request = new RefreshTokenRequest("   ");

            // Act
            var result = await AuthEndpoints_TestHelper.RefreshToken(request, _authService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        }

        #endregion

        #region ValidateToken Tests

        [Test]
        public async Task ValidateToken_WithValidToken_ReturnsOkWithUserId()
        {
            // Arrange
            var authorization = "Bearer valid-token-here";
            _authService.ValidateTokenAsync("valid-token-here", Arg.Any<CancellationToken>())
                .Returns(123L);

            // Act
            var result = await AuthEndpoints_TestHelper.ValidateToken(authorization, _authService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsValid).IsTrue();
            await Assert.That(result.Value.UserId).IsEqualTo(123);
        }

        [Test]
        public async Task ValidateToken_WithInvalidToken_ReturnsOkWithInvalidResult()
        {
            // Arrange
            var authorization = "Bearer invalid-token";
            _authService.ValidateTokenAsync("invalid-token", Arg.Any<CancellationToken>())
                .Returns((long?)null);

            // Act
            var result = await AuthEndpoints_TestHelper.ValidateToken(authorization, _authService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsValid).IsFalse();
            await Assert.That(result.Value.UserId).IsNull();
        }

        [Test]
        public async Task ValidateToken_WithMissingAuthorization_ReturnsOkWithInvalidResult()
        {
            // Arrange
            string? authorization = null;

            // Act
            var result = await AuthEndpoints_TestHelper.ValidateToken(authorization, _authService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsValid).IsFalse();
            await Assert.That(result.Value.UserId).IsNull();
        }

        [Test]
        public async Task ValidateToken_WithoutBearerPrefix_ReturnsOkWithInvalidResult()
        {
            // Arrange
            var authorization = "token-without-bearer";

            // Act
            var result = await AuthEndpoints_TestHelper.ValidateToken(authorization, _authService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsValid).IsFalse();
        }

        [Test]
        public async Task ValidateToken_WithEmptyAuthorization_ReturnsOkWithInvalidResult()
        {
            // Arrange
            var authorization = "";

            // Act
            var result = await AuthEndpoints_TestHelper.ValidateToken(authorization, _authService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsValid).IsFalse();
        }

        #endregion

        #region Helper Methods

        private static ClaimsPrincipal CreateClaimsPrincipal(long userId)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods via reflection.
    /// </summary>
    internal static class AuthEndpoints_TestHelper
    {
        public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> Login(
            LoginRequest request, IAuthService authService)
        {
            var method = typeof(LibraFoto.Modules.Auth.Endpoints.AuthEndpoints)
                .GetMethod("Login", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { request, authService, CancellationToken.None });
            return await (Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>>)result!;
        }

        public static async Task<Results<Ok, UnauthorizedHttpResult>> Logout(
            ClaimsPrincipal user, IAuthService authService)
        {
            var method = typeof(LibraFoto.Modules.Auth.Endpoints.AuthEndpoints)
                .GetMethod("Logout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { user, authService, CancellationToken.None });
            return await (Task<Results<Ok, UnauthorizedHttpResult>>)result!;
        }

        public static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>> GetCurrentUser(
            ClaimsPrincipal user, IAuthService authService)
        {
            var method = typeof(LibraFoto.Modules.Auth.Endpoints.AuthEndpoints)
                .GetMethod("GetCurrentUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { user, authService, CancellationToken.None });
            return await (Task<Results<Ok<UserDto>, UnauthorizedHttpResult, NotFound>>)result!;
        }

        public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> RefreshToken(
            RefreshTokenRequest request, IAuthService authService)
        {
            var method = typeof(LibraFoto.Modules.Auth.Endpoints.AuthEndpoints)
                .GetMethod("RefreshToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { request, authService, CancellationToken.None });
            return await (Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>>)result!;
        }

        public static async Task<Ok<TokenValidationResult>> ValidateToken(
            string? authorization, IAuthService authService)
        {
            var method = typeof(LibraFoto.Modules.Auth.Endpoints.AuthEndpoints)
                .GetMethod("ValidateToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object?[] { authorization, authService, CancellationToken.None });
            return await (Task<Ok<TokenValidationResult>>)result!;
        }
    }
}
