namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// Interface for token storage. Implementations should be thread-safe.
/// This abstraction allows for different storage backends (in-memory, Redis, database, etc.)
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Stores a refresh token for a user.
    /// </summary>
    void StoreRefreshToken(string token, long userId, DateTime expiresAt);

    /// <summary>
    /// Retrieves the user ID and expiration for a refresh token.
    /// </summary>
    bool TryGetRefreshToken(string token, out long userId, out DateTime expiresAt);

    /// <summary>
    /// Removes a refresh token.
    /// </summary>
    void RemoveRefreshToken(string token);

    /// <summary>
    /// Removes all refresh tokens for a user.
    /// </summary>
    void RemoveAllUserRefreshTokens(long userId);

    /// <summary>
    /// Gets all refresh tokens for a user.
    /// </summary>
    IEnumerable<string> GetUserRefreshTokens(long userId);

    /// <summary>
    /// Marks a JWT token as invalidated.
    /// </summary>
    void InvalidateToken(string token);

    /// <summary>
    /// Checks if a JWT token has been invalidated.
    /// </summary>
    bool IsTokenInvalidated(string token);

    /// <summary>
    /// Clears all stored tokens. For testing purposes only.
    /// </summary>
    void Clear();
}
