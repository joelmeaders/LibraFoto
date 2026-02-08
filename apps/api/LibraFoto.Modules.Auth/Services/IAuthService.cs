using LibraFoto.Modules.Auth.Models;

namespace LibraFoto.Modules.Auth.Services
{
    /// <summary>
    /// Service interface for authentication operations.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a user with username and password.
        /// </summary>
        /// <param name="request">Login credentials.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login response with tokens and user info, or null if authentication fails.</returns>
        Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs out the current user and invalidates their tokens.
        /// </summary>
        /// <param name="userId">The ID of the user to log out.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task LogoutAsync(long userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current authenticated user's information.
        /// </summary>
        /// <param name="userId">The ID of the current user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>User DTO or null if not found.</returns>
        Task<UserDto?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a JWT token and returns the associated user ID.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The user ID if valid, null otherwise.</returns>
        Task<long?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes an access token using a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>New login response with fresh tokens, or null if refresh fails.</returns>
        Task<LoginResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}
