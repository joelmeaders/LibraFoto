namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Configuration for Google Photos storage provider.
/// </summary>
public class GooglePhotosConfiguration
{
    /// <summary>
    /// Client ID for Google OAuth.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for Google OAuth.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for accessing Google Photos API.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Access token (cached, will expire).
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime? AccessTokenExpiry { get; set; }

    /// <summary>
    /// OAuth scopes granted for the stored refresh token.
    /// </summary>
    public string[]? GrantedScopes { get; set; }

    /// <summary>
    /// Whether to cache files locally or stream on demand.
    /// </summary>
    public bool EnableLocalCache { get; set; } = true;

    /// <summary>
    /// Maximum cache size in bytes (default 5GB).
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 5L * 1024 * 1024 * 1024;
}
