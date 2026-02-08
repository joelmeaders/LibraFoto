using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Endpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace LibraFoto.Tests.Modules.Auth.Endpoints
{
    /// <summary>
    /// Tests for SetupEndpoints - initial setup endpoints.
    /// </summary>
    public class SetupEndpointsTests
    {
        private ISetupService _setupService = null!;

        [Before(Test)]
        public void Setup()
        {
            _setupService = Substitute.For<ISetupService>();
        }

        #region GetSetupStatus Tests

        [Test]
        public async Task GetSetupStatus_WhenSetupRequired_ReturnsOkWithStatus()
        {
            // Arrange
            var expectedStatus = new SetupStatusResponse(
                true,
                "Initial setup is required. No users exist in the system.");
            _setupService.GetSetupStatusAsync(Arg.Any<CancellationToken>())
                .Returns(expectedStatus);

            // Act
            var result = await SetupEndpoints_TestHelper.GetSetupStatus(_setupService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsSetupRequired).IsTrue();
            await Assert.That(result.Value.Message).Contains("No users exist");
        }

        [Test]
        public async Task GetSetupStatus_WhenSetupComplete_ReturnsOkWithStatus()
        {
            // Arrange
            var expectedStatus = new SetupStatusResponse(false, "Setup is complete.");
            _setupService.GetSetupStatusAsync(Arg.Any<CancellationToken>())
                .Returns(expectedStatus);

            // Act
            var result = await SetupEndpoints_TestHelper.GetSetupStatus(_setupService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.IsSetupRequired).IsFalse();
        }

        #endregion

        #region CompleteSetup Tests

        [Test]
        public async Task CompleteSetup_WithValidRequest_ReturnsOkWithLoginResponse()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "password123");
            var loginResponse = new LoginResponse(
                "token",
                "refreshToken",
                DateTime.UtcNow.AddHours(1),
                new UserDto(1, "admin@example.com", UserRole.Admin, DateTime.UtcNow, null)
            );
            _setupService.IsSetupRequiredAsync(Arg.Any<CancellationToken>())
                .Returns(true);
            _setupService.CompleteSetupAsync(request, Arg.Any<CancellationToken>())
                .Returns(loginResponse);

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<LoginResponse>>();
            var okResult = (Ok<LoginResponse>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.User.Role).IsEqualTo(UserRole.Admin);
        }

        [Test]
        public async Task CompleteSetup_WithEmptyEmail_ReturnsValidationProblem()
        {
            // Arrange
            var request = new SetupRequest("", "password123");

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("email");
        }

        [Test]
        public async Task CompleteSetup_WithWhitespaceEmail_ReturnsValidationProblem()
        {
            // Arrange
            var request = new SetupRequest("   ", "password123");

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
        }

        [Test]
        public async Task CompleteSetup_WithEmptyPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "");

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
        }

        [Test]
        public async Task CompleteSetup_WithShortPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "12345");

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
            await Assert.That(validationResult.ProblemDetails.Errors["password"][0]).Contains("at least 6 characters");
        }

        [Test]
        public async Task CompleteSetup_WithExactly6CharPassword_IsValid()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "123456");
            var loginResponse = new LoginResponse(
                "token",
                "refreshToken",
                DateTime.UtcNow.AddHours(1),
                new UserDto(1, "admin@example.com", UserRole.Admin, DateTime.UtcNow, null)
            );
            _setupService.IsSetupRequiredAsync(Arg.Any<CancellationToken>())
                .Returns(true);
            _setupService.CompleteSetupAsync(request, Arg.Any<CancellationToken>())
                .Returns(loginResponse);

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<LoginResponse>>();
        }

        [Test]
        public async Task CompleteSetup_WhenSetupAlreadyCompleted_ReturnsConflict()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "password123");
            _setupService.IsSetupRequiredAsync(Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Conflict<ApiError>>();
            var conflictResult = (Conflict<ApiError>)result.Result;
            await Assert.That(conflictResult.Value).IsNotNull();
            await Assert.That(conflictResult.Value!.Code).IsEqualTo("SETUP_COMPLETED");
        }

        [Test]
        public async Task CompleteSetup_WhenServiceReturnsNull_ReturnsConflict()
        {
            // Arrange
            var request = new SetupRequest("admin@example.com", "password123");
            _setupService.IsSetupRequiredAsync(Arg.Any<CancellationToken>())
                .Returns(true);
            _setupService.CompleteSetupAsync(request, Arg.Any<CancellationToken>())
                .Returns((LoginResponse?)null);

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Conflict<ApiError>>();
            var conflictResult = (Conflict<ApiError>)result.Result;
            await Assert.That(conflictResult.Value).IsNotNull();
            await Assert.That(conflictResult.Value!.Code).IsEqualTo("SETUP_FAILED");
        }

        [Test]
        public async Task CompleteSetup_WithMultipleValidationErrors_ReturnsAllErrors()
        {
            // Arrange
            var request = new SetupRequest("", "12");

            // Act
            var result = await SetupEndpoints_TestHelper.CompleteSetup(request, _setupService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("email");
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods.
    /// </summary>
    internal static class SetupEndpoints_TestHelper
    {
        public static async Task<Ok<SetupStatusResponse>> GetSetupStatus(ISetupService service)
        {
            var method = typeof(SetupEndpoints)
                .GetMethod("GetSetupStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { service, CancellationToken.None });
            return await (Task<Ok<SetupStatusResponse>>)result!;
        }

        public static async Task<Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>> CompleteSetup(
            SetupRequest request, ISetupService service)
        {
            var method = typeof(SetupEndpoints)
                .GetMethod("CompleteSetup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { request, service, CancellationToken.None });
            return await (Task<Results<Ok<LoginResponse>, Conflict<ApiError>, ValidationProblem>>)result!;
        }
    }
}
