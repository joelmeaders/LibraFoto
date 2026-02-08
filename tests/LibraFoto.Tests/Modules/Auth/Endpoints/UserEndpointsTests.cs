using System.Security.Claims;
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
    /// Tests for UserEndpoints - user management endpoints.
    /// </summary>
    public class UserEndpointsTests
    {
        private IUserService _userService = null!;

        [Before(Test)]
        public void Setup()
        {
            _userService = Substitute.For<IUserService>();
        }

        #region GetUsers Tests

        [Test]
        public async Task GetUsers_WithDefaultParams_ReturnsPagedResult()
        {
            // Arrange
            var users = new[]
            {
                CreateUserDto(1, "user1@example.com", UserRole.Editor),
                CreateUserDto(2, "user2@example.com", UserRole.Guest)
            };
            _userService.GetUsersAsync(1, 20, Arg.Any<CancellationToken>())
                .Returns((users, 2));

            // Act
            var result = await UserEndpoints_TestHelper.GetUsers(0, 0, _userService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Data.Count()).IsEqualTo(2);
            await Assert.That(result.Value.Pagination.Page).IsEqualTo(1);
            await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(20);
        }

        [Test]
        public async Task GetUsers_WithCustomPagination_ReturnsCorrectPage()
        {
            // Arrange
            var users = new[] { CreateUserDto(3, "user3@example.com", UserRole.Admin) };
            _userService.GetUsersAsync(2, 10, Arg.Any<CancellationToken>())
                .Returns((users, 15));

            // Act
            var result = await UserEndpoints_TestHelper.GetUsers(2, 10, _userService);

            // Assert
            await Assert.That(result.Value).IsNotNull();
            await Assert.That(result.Value!.Pagination.Page).IsEqualTo(2);
            await Assert.That(result.Value.Pagination.PageSize).IsEqualTo(10);
            await Assert.That(result.Value.Pagination.TotalItems).IsEqualTo(15);
            await Assert.That(result.Value.Pagination.TotalPages).IsEqualTo(2);
        }

        [Test]
        public async Task GetUsers_WithExcessivePageSize_ClampsTo20()
        {
            // Arrange
            var users = Array.Empty<UserDto>();
            _userService.GetUsersAsync(1, 20, Arg.Any<CancellationToken>())
                .Returns((users, 0));

            // Act
            await UserEndpoints_TestHelper.GetUsers(1, 500, _userService);

            // Assert should use default 20, not 100
            await _userService.Received(1).GetUsersAsync(1, 20, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetUsers_WithNegativePageSize_UsesDefault()
        {
            // Arrange
            var users = Array.Empty<UserDto>();
            _userService.GetUsersAsync(1, 20, Arg.Any<CancellationToken>())
                .Returns((users, 0));

            // Act
            await UserEndpoints_TestHelper.GetUsers(1, -5, _userService);

            // Assert
            await _userService.Received(1).GetUsersAsync(1, 20, Arg.Any<CancellationToken>());
        }

        #endregion

        #region GetUserById Tests

        [Test]
        public async Task GetUserById_WithValidId_ReturnsOkWithUser()
        {
            // Arrange
            var user = CreateUserDto(100, "test@example.com", UserRole.Editor);
            _userService.GetUserByIdAsync(100, Arg.Any<CancellationToken>())
                .Returns(user);

            // Act
            var result = await UserEndpoints_TestHelper.GetUserById(100, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UserDto>>();
            var okResult = (Ok<UserDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Id).IsEqualTo(100);
        }

        [Test]
        public async Task GetUserById_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            _userService.GetUserByIdAsync(999, Arg.Any<CancellationToken>())
                .Returns((UserDto?)null);

            // Act
            var result = await UserEndpoints_TestHelper.GetUserById(999, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound>();
        }

        #endregion

        #region CreateUser Tests

        [Test]
        public async Task CreateUser_WithValidRequest_ReturnsCreated()
        {
            // Arrange
            var request = new CreateUserRequest("new@example.com", "password123", UserRole.Editor);
            var createdUser = CreateUserDto(10, "new@example.com", UserRole.Editor);
            _userService.CreateUserAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdUser);

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<UserDto>>();
            var createdResult = (Created<UserDto>)result.Result;
            await Assert.That(createdResult.Location).IsEqualTo("/api/admin/users/10");
            await Assert.That(createdResult.Value).IsNotNull();
        }

        [Test]
        public async Task CreateUser_WithEmptyEmail_ReturnsValidationProblem()
        {
            // Arrange
            var request = new CreateUserRequest("", "password123", UserRole.Editor);

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("email");
        }

        [Test]
        public async Task CreateUser_WithEmptyPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new CreateUserRequest("test@example.com", "", UserRole.Editor);

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
        }

        [Test]
        public async Task CreateUser_WithShortPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new CreateUserRequest("test@example.com", "12345", UserRole.Editor);

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
        }

        [Test]
        public async Task CreateUser_WithExactly6CharPassword_IsValid()
        {
            // Arrange
            var request = new CreateUserRequest("test@example.com", "123456", UserRole.Editor);
            var createdUser = CreateUserDto(10, "test@example.com", UserRole.Editor);
            _userService.CreateUserAsync(request, Arg.Any<CancellationToken>())
                .Returns(createdUser);

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Created<UserDto>>();
        }

        [Test]
        public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
        {
            // Arrange
            var request = new CreateUserRequest("existing@example.com", "password123", UserRole.Editor);
            _userService.When(x => x.CreateUserAsync(request, Arg.Any<CancellationToken>()))
                .Do(x => throw new InvalidOperationException("User already exists"));

            // Act
            var result = await UserEndpoints_TestHelper.CreateUser(request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Conflict<ApiError>>();
            var conflictResult = (Conflict<ApiError>)result.Result;
            await Assert.That(conflictResult.Value).IsNotNull();
            await Assert.That(conflictResult.Value!.Code).IsEqualTo("CREATE_FAILED");
        }

        #endregion

        #region UpdateUser Tests

        [Test]
        public async Task UpdateUser_WithValidRequest_ReturnsOkWithUpdatedUser()
        {
            // Arrange
            var request = new UpdateUserRequest("updated@example.com", null, UserRole.Admin);
            var updatedUser = CreateUserDto(50, "updated@example.com", UserRole.Admin);
            _userService.UpdateUserAsync(50, request, Arg.Any<CancellationToken>())
                .Returns(updatedUser);

            // Act
            var result = await UserEndpoints_TestHelper.UpdateUser(50, request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UserDto>>();
            var okResult = (Ok<UserDto>)result.Result;
            await Assert.That(okResult.Value).IsNotNull();
            await Assert.That(okResult.Value!.Email).IsEqualTo("updated@example.com");
        }

        [Test]
        public async Task UpdateUser_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateUserRequest("test@example.com", null, null);
            _userService.UpdateUserAsync(999, request, Arg.Any<CancellationToken>())
                .Returns((UserDto?)null);

            // Act
            var result = await UserEndpoints_TestHelper.UpdateUser(999, request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound>();
        }

        [Test]
        public async Task UpdateUser_WithShortPassword_ReturnsValidationProblem()
        {
            // Arrange
            var request = new UpdateUserRequest(null, "12345", null);

            // Act
            var result = await UserEndpoints_TestHelper.UpdateUser(50, request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<ValidationProblem>();
            var validationResult = (ValidationProblem)result.Result;
            await Assert.That(validationResult.ProblemDetails.Errors).ContainsKey("password");
        }

        [Test]
        public async Task UpdateUser_WithNullPassword_IsValid()
        {
            // Arrange
            var request = new UpdateUserRequest("test@example.com", null, UserRole.Guest);
            var updatedUser = CreateUserDto(50, "test@example.com", UserRole.Guest);
            _userService.UpdateUserAsync(50, request, Arg.Any<CancellationToken>())
                .Returns(updatedUser);

            // Act
            var result = await UserEndpoints_TestHelper.UpdateUser(50, request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Ok<UserDto>>();
        }

        [Test]
        public async Task UpdateUser_WhenServiceThrows_ReturnsConflict()
        {
            // Arrange
            var request = new UpdateUserRequest("duplicate@example.com", null, null);
            _userService.When(x => x.UpdateUserAsync(50, request, Arg.Any<CancellationToken>()))
                .Do(x => throw new InvalidOperationException("Email already exists"));

            // Act
            var result = await UserEndpoints_TestHelper.UpdateUser(50, request, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Conflict<ApiError>>();
        }

        #endregion

        #region DeleteUser Tests

        [Test]
        public async Task DeleteUser_WithValidId_ReturnsNoContent()
        {
            // Arrange
            var user = CreateClaimsPrincipal(100);
            _userService.DeleteUserAsync(50, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await UserEndpoints_TestHelper.DeleteUser(50, user, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NoContent>();
        }

        [Test]
        public async Task DeleteUser_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var user = CreateClaimsPrincipal(100);
            _userService.DeleteUserAsync(999, Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await UserEndpoints_TestHelper.DeleteUser(999, user, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NotFound>();
        }

        [Test]
        public async Task DeleteUser_WhenDeletingSelf_ReturnsConflict()
        {
            // Arrange
            var user = CreateClaimsPrincipal(50);

            // Act
            var result = await UserEndpoints_TestHelper.DeleteUser(50, user, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<Conflict<ApiError>>();
            var conflictResult = (Conflict<ApiError>)result.Result;
            await Assert.That(conflictResult.Value).IsNotNull();
            await Assert.That(conflictResult.Value!.Code).IsEqualTo("CANNOT_DELETE_SELF");
        }

        [Test]
        public async Task DeleteUser_WithNoUserIdClaim_CanDeleteAnyUser()
        {
            // Arrange - user has no NameIdentifier claim
            var user = new ClaimsPrincipal(new ClaimsIdentity());
            _userService.DeleteUserAsync(50, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await UserEndpoints_TestHelper.DeleteUser(50, user, _userService);

            // Assert - should allow deletion since no claim to compare
            await Assert.That(result.Result).IsTypeOf<NoContent>();
        }

        [Test]
        public async Task DeleteUser_WithInvalidUserIdClaim_CanDeleteAnyUser()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "not-a-number")
            };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            _userService.DeleteUserAsync(50, Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await UserEndpoints_TestHelper.DeleteUser(50, user, _userService);

            // Assert
            await Assert.That(result.Result).IsTypeOf<NoContent>();
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

        private static UserDto CreateUserDto(long id, string email, UserRole role)
        {
            return new UserDto(id, email, role, DateTime.UtcNow, null);
        }

        #endregion
    }

    /// <summary>
    /// Test helper to access private endpoint methods.
    /// </summary>
    internal static class UserEndpoints_TestHelper
    {
        public static async Task<Ok<PagedResult<UserDto>>> GetUsers(
            int page, int pageSize, IUserService service)
        {
            var method = typeof(UserEndpoints)
                .GetMethod("GetUsers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { page, pageSize, service, CancellationToken.None });
            return await (Task<Ok<PagedResult<UserDto>>>)result!;
        }

        public static async Task<Results<Ok<UserDto>, NotFound>> GetUserById(
            long id, IUserService service)
        {
            var method = typeof(UserEndpoints)
                .GetMethod("GetUserById", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, service, CancellationToken.None });
            return await (Task<Results<Ok<UserDto>, NotFound>>)result!;
        }

        public static async Task<Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>> CreateUser(
            CreateUserRequest request, IUserService service)
        {
            var method = typeof(UserEndpoints)
                .GetMethod("CreateUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { request, service, CancellationToken.None });
            return await (Task<Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>>)result!;
        }

        public static async Task<Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>> UpdateUser(
            long id, UpdateUserRequest request, IUserService service)
        {
            var method = typeof(UserEndpoints)
                .GetMethod("UpdateUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, request, service, CancellationToken.None });
            return await (Task<Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>>)result!;
        }

        public static async Task<Results<NoContent, NotFound, Conflict<ApiError>>> DeleteUser(
            long id, ClaimsPrincipal user, IUserService service)
        {
            var method = typeof(UserEndpoints)
                .GetMethod("DeleteUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var result = method!.Invoke(null, new object[] { id, user, service, CancellationToken.None });
            return await (Task<Results<NoContent, NotFound, Conflict<ApiError>>>)result!;
        }
    }
}
