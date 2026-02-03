using LibraFoto.Modules.Auth.Models;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// Service interface for user management operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets all users with optional pagination.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of users.</returns>
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User DTO or null if not found.</returns>
    Task<UserDto?> GetUserByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User DTO or null if not found.</returns>
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request">User creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created user DTO.</returns>
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="id">The user ID to update.</param>
    /// <param name="request">Update request with changed fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated user DTO or null if not found.</returns>
    Task<UserDto?> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="id">The user ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteUserAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of users.</returns>
    Task<int> GetUserCountAsync(CancellationToken cancellationToken = default);
}
