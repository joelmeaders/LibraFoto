using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using LibraFoto.Data;
using LibraFoto.Modules.Storage.Models;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Picker;

internal static class GooglePhotosPickerHelper
{
    internal const string PickerScope = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";

    internal static PickerSessionDto MapSessionDto(PickerSessionResponse session)
    {
        return new PickerSessionDto
        {
            SessionId = session.Id ?? string.Empty,
            PickerUri = session.PickerUri ?? string.Empty,
            MediaItemsSet = session.MediaItemsSet,
            ExpireTime = session.ExpireTime,
            PollingConfig = session.PollingConfig == null
                ? null
                : new PickerPollingConfig
                {
                    PollInterval = session.PollingConfig.PollInterval,
                    TimeoutIn = session.PollingConfig.TimeoutIn
                }
        };
    }

    internal static PickedMediaItemDto MapItemDto(
        long providerId,
        string sessionId,
        PickedMediaItemResponse item)
    {
        var width = item.MediaFile?.MediaFileMetadata?.Width;
        var height = item.MediaFile?.MediaFileMetadata?.Height;
        var videoStatus = item.MediaFile?.MediaFileMetadata?.VideoMetadata?.ProcessingStatus;

        var thumbnailUrl = $"/api/storage/google-photos/{providerId}/picker/sessions/{sessionId}/items/{item.Id}/thumbnail?width=300&height=300";

        return new PickedMediaItemDto
        {
            Id = item.Id ?? string.Empty,
            Type = item.Type ?? "TYPE_UNSPECIFIED",
            MimeType = item.MediaFile?.MimeType,
            Filename = item.MediaFile?.Filename,
            Width = width,
            Height = height,
            CreateTime = item.CreateTime,
            VideoProcessingStatus = videoStatus,
            ThumbnailUrl = thumbnailUrl
        };
    }

    internal static GooglePhotosConfiguration? ParseConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GooglePhotosConfiguration>(json);
        }
        catch
        {
            return null;
        }
    }

    internal static bool TryGetOAuthCredentials(
        GooglePhotosConfiguration? config,
        out string? clientId,
        out string? clientSecret,
        out string? refreshToken,
        out string error)
    {
        clientId = config?.ClientId;
        clientSecret = config?.ClientSecret;
        refreshToken = config?.RefreshToken;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            error = "Google Photos client ID and secret must be configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            error = "Google Photos refresh token is missing. Please reconnect the provider.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static async Task PersistConfigAsync(
        Data.Entities.StorageProvider provider,
        GooglePhotosConfiguration config,
        LibraFotoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        provider.Configuration = JsonSerializer.Serialize(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<string?> EnsureAccessTokenAsync(
        GooglePhotosConfiguration config,
        string clientId,
        string clientSecret,
        string refreshToken,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var scopeString = config.GrantedScopes is { Length: > 0 }
            ? string.Join(" ", config.GrantedScopes)
            : PickerScope;

        var expiresInSeconds = GetExpiresInSeconds(config.AccessTokenExpiry);
        var tokenResponse = new TokenResponse
        {
            RefreshToken = refreshToken,
            AccessToken = config.AccessToken,
            ExpiresInSeconds = expiresInSeconds,
            Scope = scopeString,
            IssuedUtc = config.AccessTokenExpiry?.AddSeconds(-(expiresInSeconds ?? 3600)) ?? DateTime.UtcNow.AddHours(-1)
        };

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new Google.Apis.Auth.OAuth2.ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            Scopes = [PickerScope],
            DataStore = null
        });

        var credential = new UserCredential(flow, "user", tokenResponse);

        if (tokenResponse.IsStale)
        {
            logger.LogDebug("Picker token is stale, refreshing...");
            await credential.RefreshTokenAsync(cancellationToken);
            credential.Token.Scope = scopeString;
            config.AccessToken = credential.Token.AccessToken;
            config.AccessTokenExpiry = GetSafeExpiry(DateTime.UtcNow, credential.Token.ExpiresInSeconds);
            config.GrantedScopes = (credential.Token.Scope ?? scopeString)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
        else if (string.IsNullOrWhiteSpace(config.AccessToken))
        {
            config.AccessToken = credential.Token.AccessToken;
        }

        return credential.Token.AccessToken;
    }

    internal static string GetExtensionFromMimeType(string? mimeType) => mimeType?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/heic" => ".heic",
        "image/heif" => ".heif",
        "video/mp4" => ".mp4",
        "video/quicktime" => ".mov",
        "video/x-msvideo" => ".avi",
        _ => ".jpg"
    };

    private static long? GetExpiresInSeconds(DateTime? expiryUtc)
    {
        if (!expiryUtc.HasValue)
        {
            return null;
        }

        var expiry = DateTime.SpecifyKind(expiryUtc.Value, DateTimeKind.Utc);
        var seconds = (expiry - DateTime.UtcNow).TotalSeconds;
        if (seconds <= 0 || seconds > TimeSpan.FromDays(30).TotalSeconds)
        {
            return null;
        }

        return (long)seconds;
    }

    private static DateTime GetSafeExpiry(DateTime now, long? expiresInSeconds)
    {
        var seconds = expiresInSeconds ?? 3600;
        if (seconds <= 0 || seconds > TimeSpan.FromDays(30).TotalSeconds)
        {
            seconds = 3600;
        }

        return now.AddSeconds(seconds);
    }
}
