using System.Collections.Concurrent;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// In-memory implementation of token storage using concurrent collections.
/// Thread-safe for concurrent access. Registered as a singleton.
///
/// Note: This implementation loses all tokens on application restart.
/// For production with multiple instances, consider Redis or database-backed storage.
/// </summary>
public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, (long UserId, DateTime ExpiresAt)> _refreshTokens = new();
    private readonly ConcurrentDictionary<long, ConcurrentBag<string>> _userRefreshTokens = new();
    private readonly ConcurrentBag<string> _invalidatedTokens = [];

    public void StoreRefreshToken(string token, long userId, DateTime expiresAt)
    {
        _refreshTokens[token] = (userId, expiresAt);

        var userTokens = _userRefreshTokens.GetOrAdd(userId, _ => []);
        userTokens.Add(token);
    }

    public bool TryGetRefreshToken(string token, out long userId, out DateTime expiresAt)
    {
        if (_refreshTokens.TryGetValue(token, out var value))
        {
            userId = value.UserId;
            expiresAt = value.ExpiresAt;
            return true;
        }

        userId = default;
        expiresAt = default;
        return false;
    }

    public void RemoveRefreshToken(string token)
    {
        if (_refreshTokens.TryRemove(token, out var value))
        {
            // Remove from user's token list
            if (_userRefreshTokens.TryGetValue(value.UserId, out var userTokens))
            {
                // ConcurrentBag doesn't support removal, so we'll leave stale references
                // They'll be filtered out when accessed based on _refreshTokens
            }
        }
    }

    public void RemoveAllUserRefreshTokens(long userId)
    {
        if (_userRefreshTokens.TryRemove(userId, out var userTokens))
        {
            foreach (var token in userTokens)
            {
                _refreshTokens.TryRemove(token, out _);
            }
        }
    }

    public IEnumerable<string> GetUserRefreshTokens(long userId)
    {
        if (_userRefreshTokens.TryGetValue(userId, out var tokens))
        {
            // Filter out tokens that have been removed
            return tokens.Where(t => _refreshTokens.ContainsKey(t));
        }
        return Enumerable.Empty<string>();
    }

    public void InvalidateToken(string token)
    {
        _invalidatedTokens.Add(token);
    }

    public bool IsTokenInvalidated(string token)
    {
        return _invalidatedTokens.Contains(token);
    }

    public void Clear()
    {
        _refreshTokens.Clear();
        _userRefreshTokens.Clear();
        _invalidatedTokens.Clear();
    }
}
