using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Endpoints;

/// <summary>
/// Endpoints for Google Photos Picker API integration.
/// </summary>
public static class GooglePhotosPickerEndpoints
{
    private const string PickerScope = "https://www.googleapis.com/auth/photospicker.mediaitems.readonly";
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public static IEndpointRouteBuilder MapGooglePhotosPickerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/storage/google-photos")
            .WithTags("Storage - Google Photos Picker");

        group.MapPost("/{providerId:long}/picker/start", StartPickerSession)
            .WithName("StartGooglePhotosPickerSession")
            .WithSummary("Start a Google Photos picker session");

        group.MapGet("/{providerId:long}/picker/sessions/{sessionId}", GetPickerSession)
            .WithName("GetGooglePhotosPickerSession")
            .WithSummary("Get picker session status");

        group.MapGet("/{providerId:long}/picker/sessions/{sessionId}/items", GetPickerItems)
            .WithName("GetGooglePhotosPickerItems")
            .WithSummary("Get picked media items");

        group.MapPost("/{providerId:long}/picker/sessions/{sessionId}/import", ImportPickerItems)
            .WithName("ImportGooglePhotosPickerItems")
            .WithSummary("Import picked media items into LibraFoto");

        group.MapDelete("/{providerId:long}/picker/sessions/{sessionId}", DeletePickerSession)
            .WithName("DeleteGooglePhotosPickerSession")
            .WithSummary("Delete a picker session");

        group.MapGet("/{providerId:long}/picker/sessions/{sessionId}/items/{itemId}/thumbnail", GetPickerThumbnail)
            .WithName("GetGooglePhotosPickerThumbnail")
            .WithSummary("Get a picker item thumbnail");

        return app;
    }

    private static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> StartPickerSession(
        long providerId,
        [FromBody] StartPickerSessionRequest? request,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var session = await pickerService.CreateSessionAsync(accessToken, request?.MaxItemCount, cancellationToken);
        if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.PickerUri))
        {
            return TypedResults.BadRequest(new ApiError("PICKER_FAILED", "Picker session response was incomplete."));
        }

        var entity = new PickerSession
        {
            ProviderId = providerId,
            SessionId = session.Id,
            PickerUri = session.PickerUri,
            MediaItemsSet = session.MediaItemsSet,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = session.ExpireTime
        };

        dbContext.PickerSessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapSessionDto(session));
    }

    private static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerSession(
        long providerId,
        string sessionId,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var session = await pickerService.GetSessionAsync(sessionId, accessToken, cancellationToken);

        var entity = await dbContext.PickerSessions
            .FirstOrDefaultAsync(s => s.ProviderId == providerId && s.SessionId == sessionId, cancellationToken);

        if (entity != null)
        {
            entity.MediaItemsSet = session.MediaItemsSet;
            entity.ExpiresAt = session.ExpireTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(MapSessionDto(session));
    }

    private static async Task<Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerItems(
        long providerId,
        string sessionId,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var items = await pickerService.ListMediaItemsAsync(sessionId, accessToken, cancellationToken);
        var result = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => MapItemDto(providerId, sessionId, item))
            .ToArray();

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>> ImportPickerItems(
        long providerId,
        string sessionId,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ICacheService cacheService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var items = await pickerService.ListMediaItemsAsync(sessionId, accessToken, cancellationToken);
        var imported = 0;
        var failed = 0;

        var importContext = new PickerImportContext(
            providerId,
            sessionId,
            accessToken,
            pickerService,
            cacheService,
            dbContext,
            logger);

        foreach (var item in items)
        {
            var success = await TryImportPickerItemAsync(
                importContext,
                item,
                cancellationToken);

            if (success)
            {
                imported++;
            }
            else
            {
                failed++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ImportPickerItemsResponse
        {
            Imported = imported,
            Failed = failed
        });
    }

    private static async Task<bool> TryImportPickerItemAsync(
        PickerImportContext context,
        PickedMediaItemResponse item,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.MediaFile?.BaseUrl == null)
        {
            return false;
        }

        try
        {
            var isVideo = string.Equals(item.Type, "VIDEO", StringComparison.OrdinalIgnoreCase);
            var (downloadStream, contentType) = await context.PickerService.DownloadMediaItemAsync(
                item.MediaFile.BaseUrl,
                context.AccessToken,
                isVideo,
                maxWidth: 4096,
                maxHeight: 4096,
                cancellationToken);

            await using var tempStream = downloadStream;
            var fileHash = await CacheService.ComputeHashAsync(tempStream, cancellationToken);
            tempStream.Position = 0;

            var cachedFile = await context.CacheService.CacheFileAsync(
                new CacheFileRequest
                {
                    FileHash = fileHash,
                    OriginalUrl = item.MediaFile.BaseUrl,
                    ProviderId = context.ProviderId,
                    ProviderFileId = item.Id,
                    PickerSessionId = context.SessionId,
                    FileStream = tempStream,
                    ContentType = contentType
                },
                cancellationToken);

            var existingPhoto = await context.DbContext.Photos
                .FirstOrDefaultAsync(p => p.ProviderId == context.ProviderId && p.ProviderFileId == item.Id, cancellationToken);

            var fileName = item.MediaFile.Filename ?? item.Id;
            var width = item.MediaFile.MediaFileMetadata?.Width ?? 0;
            var height = item.MediaFile.MediaFileMetadata?.Height ?? 0;

            if (existingPhoto == null)
            {
                var photo = new Photo
                {
                    Filename = fileName,
                    OriginalFilename = fileName,
                    FilePath = cachedFile.LocalPath,
                    FileSize = cachedFile.FileSize,
                    Width = width,
                    Height = height,
                    MediaType = isVideo ? MediaType.Video : MediaType.Photo,
                    Duration = null,
                    DateTaken = item.CreateTime,
                    DateAdded = DateTime.UtcNow,
                    ProviderId = context.ProviderId,
                    ProviderFileId = item.Id
                };

                context.DbContext.Photos.Add(photo);
            }
            else
            {
                existingPhoto.FileSize = cachedFile.FileSize;
                existingPhoto.Width = width;
                existingPhoto.Height = height;
                existingPhoto.FilePath = cachedFile.LocalPath;
            }

            return true;
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to import picker item {ItemId}", item.Id);
            return false;
        }
    }

    private static async Task<Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>> DeletePickerSession(
        long providerId,
        string sessionId,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        await pickerService.DeleteSessionAsync(sessionId, accessToken, cancellationToken);

        var entity = await dbContext.PickerSessions
            .FirstOrDefaultAsync(s => s.ProviderId == providerId && s.SessionId == sessionId, cancellationToken);

        if (entity != null)
        {
            dbContext.PickerSessions.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>> GetPickerThumbnail(
        [AsParameters] PickerThumbnailRequest request,
        [FromServices] LibraFotoDbContext dbContext,
        [FromServices] GooglePhotosPickerService pickerService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([request.ProviderId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = ParseConfig(provider.Configuration);
        if (!TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError("MISSING_CREDENTIALS", error));
        }

        var accessToken = await EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError("OAUTH_FAILED", "Failed to refresh Google access token."));
        }

        await PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var items = await pickerService.ListMediaItemsAsync(request.SessionId, accessToken, cancellationToken);
        var item = items.FirstOrDefault(i => string.Equals(i.Id, request.ItemId, StringComparison.OrdinalIgnoreCase));
        if (item?.MediaFile?.BaseUrl == null)
        {
            return TypedResults.NotFound(new ApiError("ITEM_NOT_FOUND", "Picker item not found."));
        }

        var response = await pickerService.DownloadMediaItemAsync(
            item.MediaFile.BaseUrl,
            accessToken,
            isVideo: false,
            maxWidth: request.Width > 0 ? request.Width : 400,
            maxHeight: request.Height > 0 ? request.Height : 400,
            cancellationToken);

        return TypedResults.File(response.Stream, response.ContentType, enableRangeProcessing: true);
    }

    private static PickerSessionDto MapSessionDto(PickerSessionResponse session)
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

    private static PickedMediaItemDto MapItemDto(
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

    private static GooglePhotosConfiguration? ParseConfig(string? json)
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

    private static bool TryGetOAuthCredentials(
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

    private static async Task PersistConfigAsync(
        StorageProvider provider,
        GooglePhotosConfiguration config,
        LibraFotoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        provider.Configuration = JsonSerializer.Serialize(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string?> EnsureAccessTokenAsync(
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
            ClientSecrets = new ClientSecrets
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

public record StartPickerSessionRequest
{
    public long? MaxItemCount { get; init; }
}

public record ImportPickerItemsResponse
{
    public int Imported { get; init; }
    public int Failed { get; init; }
}

public record PickerThumbnailRequest(
    long ProviderId,
    string SessionId,
    string ItemId,
    [FromQuery] int Width,
    [FromQuery] int Height);

internal record PickerImportContext(
    long ProviderId,
    string SessionId,
    string AccessToken,
    GooglePhotosPickerService PickerService,
    ICacheService CacheService,
    LibraFotoDbContext DbContext,
    ILogger Logger);
