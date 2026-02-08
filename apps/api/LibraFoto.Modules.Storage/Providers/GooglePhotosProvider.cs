using System.Text.Json;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Providers
{
    /// <summary>
    /// Storage provider for Google Photos integration using the Picker API.
    /// Photos are selected by users through Google's Picker UI and imported locally.
    /// This provider serves files from local storage after import.
    /// </summary>
    public class GooglePhotosProvider : IStorageProvider, IOAuthProvider
    {
        /// <summary>
        /// The required scope for Google Photos Picker API.
        /// </summary>
        public const string PickerScope = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";

        private readonly ILogger<GooglePhotosProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly LibraFotoDbContext _dbContext;

        private long _providerId;
        private string _displayName = "Google Photos";
        private GooglePhotosConfiguration _config = new();

        public GooglePhotosProvider(
            ILogger<GooglePhotosProvider> logger,
            IHttpClientFactory httpClientFactory,
            LibraFotoDbContext dbContext)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public long ProviderId => _providerId;

        /// <inheritdoc />
        public StorageProviderType ProviderType => StorageProviderType.GooglePhotos;

        /// <inheritdoc />
        public string DisplayName => _displayName;

        /// <inheritdoc />
        public bool SupportsUpload => false; // Read-only via Picker

        /// <inheritdoc />
        public bool SupportsWatch => false; // User-initiated import via Picker

        /// <inheritdoc />
        public void Initialize(long providerId, string displayName, string? configuration)
        {
            _providerId = providerId;
            _displayName = displayName;

            if (!string.IsNullOrEmpty(configuration))
            {
                try
                {
                    _config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(configuration) ?? new();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Google Photos configuration, using defaults");
                    _config = new GooglePhotosConfiguration();
                }
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<StorageFileInfo>> GetFilesAsync(
            string? folderId,
            CancellationToken cancellationToken = default)
        {
            // Query photos imported from this provider via the Picker flow
            var photos = await _dbContext.Photos
                .Where(p => p.ProviderId == _providerId)
                .OrderByDescending(p => p.DateTaken ?? p.DateAdded)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} photos imported from Google Photos provider {ProviderId}", photos.Count, _providerId);

            return photos.Select(MapPhotoToStorageFileInfo);
        }

        /// <summary>
        /// Maps a Photo entity to StorageFileInfo.
        /// </summary>
        private static StorageFileInfo MapPhotoToStorageFileInfo(Photo photo)
        {
            return new StorageFileInfo
            {
                FileId = photo.ProviderFileId ?? photo.Id.ToString(),
                FileName = photo.Filename,
                FullPath = photo.FilePath,
                FileSize = photo.FileSize,
                ContentType = photo.MediaType == MediaType.Video ? "video/mp4" : "image/jpeg",
                MediaType = photo.MediaType,
                CreatedDate = photo.DateTaken ?? photo.DateAdded,
                ModifiedDate = photo.DateAdded,
                Width = photo.Width,
                Height = photo.Height,
                Duration = photo.Duration,
                IsFolder = false,
                ParentFolderId = null
            };
        }

        /// <inheritdoc />
        public async Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
        {
            using var stream = await GetFileStreamAsync(fileId, cancellationToken);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }

        /// <inheritdoc />
        public async Task<Stream> GetFileStreamAsync(string fileId, CancellationToken cancellationToken = default)
        {
            var photo = await _dbContext.Photos
                .FirstOrDefaultAsync(p => p.ProviderId == _providerId && p.ProviderFileId == fileId, cancellationToken);

            if (photo == null)
            {
                _logger.LogWarning("Photo with provider file ID {FileId} not found for provider {ProviderId}", fileId, _providerId);
                throw new FileNotFoundException($"Photo with file ID {fileId} not found");
            }

            if (!string.IsNullOrEmpty(photo.FilePath) && File.Exists(photo.FilePath))
            {
                _logger.LogDebug("Serving file {FileId} from local path {Path}", fileId, photo.FilePath);
                return new FileStream(photo.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            _logger.LogError("File not found for photo {FileId}. The file may need to be re-imported via the Picker.", fileId);
            throw new FileNotFoundException($"File for {fileId} not found. Please re-import via Google Photos Picker.");
        }

        /// <inheritdoc />
        public Task<UploadResult> UploadFileAsync(
            string fileName,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Photos provider is read-only. Use the Picker to import photos.");
        }

        /// <inheritdoc />
        public Task<bool> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Photos provider does not support deleting files from Google Photos.");
        }

        /// <inheritdoc />
        public async Task<bool> FileExistsAsync(string fileId, CancellationToken cancellationToken = default)
        {
            // Check if the photo exists in our database (imported via Picker)
            return await _dbContext.Photos
                .AnyAsync(p => p.ProviderId == _providerId && p.ProviderFileId == fileId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            // For the Picker API, we validate that we have the required credentials
            // and the correct scope for the Picker API.

            if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
            {
                _logger.LogDebug("Google Photos connection test failed: missing client credentials");
                return false;
            }

            if (string.IsNullOrEmpty(_config.RefreshToken))
            {
                _logger.LogDebug("Google Photos connection test failed: missing refresh token");
                return false;
            }

            // Check if we have the Picker API scope
            if (_config.GrantedScopes is { Length: > 0 })
            {
                var hasPickerScope = _config.GrantedScopes.Any(s =>
                    s.Equals(PickerScope, StringComparison.OrdinalIgnoreCase));

                if (!hasPickerScope)
                {
                    _logger.LogWarning(
                        "Google Photos connection test failed: missing Picker API scope. Granted scopes: {Scopes}",
                        string.Join(", ", _config.GrantedScopes));
                    return false;
                }
            }

            // Optionally validate the access token is still valid
            if (!string.IsNullOrEmpty(_config.AccessToken))
            {
                try
                {
                    var tokenInfoResponse = await _httpClient.GetAsync(
                        $"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(_config.AccessToken)}",
                        cancellationToken);

                    if (tokenInfoResponse.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("Google Photos access token is valid");
                    }
                    else
                    {
                        // Token may be expired, but we have a refresh token so connection is still valid
                        _logger.LogDebug(
                            "Google Photos access token validation returned {Status}, but refresh token is available",
                            tokenInfoResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Token validation request failed, but credentials are configured");
                }
            }

            _logger.LogDebug("Google Photos Picker API credentials are valid");
            return true;
        }

        /// <summary>
        /// Checks if granted scopes include the Picker API scope.
        /// </summary>
        public static bool HasRequiredScopes(IEnumerable<string> grantedScopes)
        {
            var scopeSet = new HashSet<string>(grantedScopes, StringComparer.OrdinalIgnoreCase);
            return scopeSet.Contains(PickerScope);
        }

        /// <inheritdoc />
        public async Task<bool> DisconnectAsync(StorageProvider providerEntity, CancellationToken cancellationToken = default)
        {
            try
            {
                // Parse configuration to get tokens for revocation
                if (!string.IsNullOrEmpty(providerEntity.Configuration))
                {
                    var config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(providerEntity.Configuration);
                    if (config != null)
                    {
                        // Revoke the tokens with Google to remove app access from user's Google account
                        var tokenToRevoke = config.RefreshToken ?? config.AccessToken;
                        if (!string.IsNullOrEmpty(tokenToRevoke))
                        {
                            await RevokeGoogleTokenAsync(tokenToRevoke, cancellationToken);
                        }

                        // Clear OAuth tokens from configuration
                        config.RefreshToken = null;
                        config.AccessToken = null;
                        config.AccessTokenExpiry = null;
                        config.GrantedScopes = null;
                        providerEntity.Configuration = JsonSerializer.Serialize(config);
                    }
                }

                // Disable the provider
                providerEntity.IsEnabled = false;

                _logger.LogInformation("Disconnected Google Photos provider {ProviderId}", providerEntity.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disconnect Google Photos provider {ProviderId}", providerEntity.Id);
                // Still disable the provider even on error
                providerEntity.IsEnabled = false;
                return false;
            }
        }

        /// <summary>
        /// Revokes an OAuth token with Google's revocation endpoint.
        /// </summary>
        private async Task RevokeGoogleTokenAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(token)}",
                    null,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully revoked Google OAuth token");
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Failed to revoke Google OAuth token. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode,
                        content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error revoking Google OAuth token (will continue with local cleanup)");
            }
        }
    }
}
