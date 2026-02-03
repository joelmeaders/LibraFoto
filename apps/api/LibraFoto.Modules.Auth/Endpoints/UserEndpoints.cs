using System.Security.Claims;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Auth.Endpoints;

/// <summary>
/// User management endpoints for admin users.
/// </summary>
public static class UserEndpoints
{
    /// <summary>
    /// Maps user management endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .WithTags("User Management")
            .RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        group.MapGet("/", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Get all users")
            .WithDescription("Returns a paginated list of all users. Admin only.");

        group.MapGet("/{id:long}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Returns a specific user by their ID. Admin only.");

        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create a new user")
            .WithDescription("Creates a new user with the specified role. Admin only.");

        group.MapPut("/{id:long}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Update a user")
            .WithDescription("Updates an existing user's information. Admin only.");

        group.MapDelete("/{id:long}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Delete a user")
            .WithDescription("Deletes a user. Cannot delete yourself. Admin only.");

        return app;
    }

    /// <summary>
    /// Gets all users with pagination.
    /// </summary>
    private static async Task<Ok<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        // Default values
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (users, totalCount) = await userService.GetUsersAsync(page, pageSize, cancellationToken);
        var userArray = users.ToArray();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pagination = new PaginationInfo(page, pageSize, totalCount, totalPages);

        return TypedResults.Ok(new PagedResult<UserDto>(userArray, pagination));
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    private static async Task<Results<Ok<UserDto>, NotFound>> GetUserById(
        long id,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken);

        if (user == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    private static async Task<Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>> CreateUser(
        [FromBody] CreateUserRequest request,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        // Validate request
        var errors = ValidateCreateUserRequest(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var user = await userService.CreateUserAsync(request, cancellationToken);
            return TypedResults.Created($"/api/admin/users/{user.Id}", user);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new ApiError("CREATE_FAILED", ex.Message));
        }
    }

    /// <summary>
    /// Updates a user.
    /// </summary>
    private static async Task<Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>> UpdateUser(
        long id,
        [FromBody] UpdateUserRequest request,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        // Validate request
        var errors = ValidateUpdateUserRequest(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var user = await userService.UpdateUserAsync(id, request, cancellationToken);

            if (user == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new ApiError("UPDATE_FAILED", ex.Message));
        }
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    private static async Task<Results<NoContent, NotFound, Conflict<ApiError>>> DeleteUser(
        long id,
        ClaimsPrincipal user,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        // Prevent self-deletion
        var currentUserIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim != null &&
            long.TryParse(currentUserIdClaim.Value, out var currentUserId) &&
            currentUserId == id)
        {
            return TypedResults.Conflict(new ApiError(
                "CANNOT_DELETE_SELF",
                "You cannot delete your own account."));
        }

        var result = await userService.DeleteUserAsync(id, cancellationToken);

        if (!result)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> ValidateCreateUserRequest(CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = new[] { "Email is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Password is required." };
        }
        else if (request.Password.Length < 6)
        {
            errors["password"] = new[] { "Password must be at least 6 characters." };
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateUserRequest(UpdateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Password != null && request.Password.Length < 6)
        {
            errors["password"] = new[] { "Password must be at least 6 characters." };
        }

        return errors;
    }
}
