using System.Text.Json;
using FastEndpoints;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.OAuth;

/// <summary>
/// Handle Google Photos OAuth callback.
/// </summary>
public sealed class GooglePhotosOAuthCallbackEndpoint : Endpoint<GooglePhotosCallbackRequest, Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>>
{
    private static readonly string[] _googlePhotosScopes =
    [
        "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
    ];

    public override void Configure()
    {
        Post("/api/storage/google-photos/{providerId:long}/callback");
        Tags("Storage - Google Photos OAuth");
        Summary(s =>
        {
            s.Summary = "Handle OAuth callback from Google";
        });
    }

    public override async Task<Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>> ExecuteAsync(
        GooglePhotosCallbackRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var configuration = Resolve<IConfiguration>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req.AuthorizationCode, dbContext, configuration, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>> HandleRequestAsync(
        long providerId,
        string authorizationCode,
        LibraFotoDbContext dbContext,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("GooglePhotosOAuth");
        try
        {
            var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

            if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
            {
                return TypedResults.NotFound(new ApiError("PROVIDER_NOT_FOUND", "Google Photos provider not found"));
            }

            GooglePhotosConfiguration? existingConfig = null;
            if (!string.IsNullOrEmpty(provider.Configuration))
            {
                try
                {
                    existingConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(provider.Configuration);
                }
                catch (JsonException)
                {
                    existingConfig = null;
                }
            }

            var clientId = existingConfig?.ClientId ?? configuration["GooglePhotos:ClientId"];
            var clientSecret = existingConfig?.ClientSecret ?? configuration["GooglePhotos:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return TypedResults.BadRequest(new ApiError(
                    "MISSING_CREDENTIALS",
                    "Google Photos client ID and secret must be configured"));
            }

            var redirectUri = configuration["GooglePhotos:RedirectUri"] ?? "http://localhost:4200/oauth/callback";

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = _googlePhotosScopes
            });

            var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                "user",
                authorizationCode,
                redirectUri,
                cancellationToken);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                return TypedResults.BadRequest(new ApiError(
                    "OAUTH_FAILED",
                    "Failed to obtain refresh token from Google. Please ensure your app is configured for offline access."));
            }

            logger.LogInformation(
                "Google OAuth token response - Scope: [{Scope}], HasRefreshToken: {HasRefresh}, ExpiresIn: {ExpiresIn}",
                tokenResponse.Scope ?? "(null)",
                !string.IsNullOrEmpty(tokenResponse.RefreshToken),
                tokenResponse.ExpiresInSeconds);

            var grantedScopes = (tokenResponse.Scope ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            logger.LogDebug(
                "Parsed granted scopes: [{Scopes}], Count: {Count}",
                string.Join(", ", grantedScopes),
                grantedScopes.Count);

            if (grantedScopes.Count == 0)
            {
                logger.LogError("Google OAuth returned no scopes - this indicates a token exchange issue");

                await RevokeTokenAsync(tokenResponse.AccessToken, logger, cancellationToken);

                return TypedResults.BadRequest(new ApiError(
                    "NO_SCOPES_GRANTED",
                    "Google did not return any granted scopes. This may indicate a problem with the OAuth configuration. " +
                    "Please verify that the Photos Library API is enabled in your Google Cloud Console."));
            }

            var missingScopes = _googlePhotosScopes
                .Where(s => !grantedScopes.Contains(s))
                .ToList();

            if (missingScopes.Count > 0)
            {
                logger.LogError(
                    "Google OAuth granted insufficient scopes. Required: [{Required}], Granted: [{Granted}], Missing: [{Missing}]",
                    string.Join(", ", _googlePhotosScopes),
                    string.Join(", ", grantedScopes),
                    string.Join(", ", missingScopes));

                await RevokeTokenAsync(tokenResponse.AccessToken, logger, cancellationToken);

                return TypedResults.BadRequest(new ApiError(
                    "INSUFFICIENT_SCOPES",
                    $"Google did not grant all required permissions. Missing scopes: {string.Join(", ", missingScopes)}. " +
                    "This can happen if your Google Cloud app is not verified. Go to your Google Account settings " +
                    "(https://myaccount.google.com/permissions), remove access for this app, then try connecting again " +
                    "and ensure you approve ALL requested permissions."));
            }

            var config = new GooglePhotosConfiguration
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                RefreshToken = tokenResponse.RefreshToken,
                AccessToken = tokenResponse.AccessToken,
                AccessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
                GrantedScopes = (tokenResponse.Scope ?? string.Join(" ", _googlePhotosScopes))
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            };

            provider.Configuration = JsonSerializer.Serialize(config);
            provider.IsEnabled = true;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Updated Google Photos provider {ProviderId} with OAuth tokens", provider.Id);

            return TypedResults.Ok(new StorageProviderDto
            {
                Id = provider.Id,
                Type = provider.Type,
                Name = provider.Name,
                IsEnabled = provider.IsEnabled,
                SupportsUpload = false,
                SupportsWatch = false,
                LastSyncDate = provider.LastSyncDate,
                PhotoCount = await dbContext.Photos.CountAsync(p => p.ProviderId == provider.Id, cancellationToken),
                IsConnected = true,
                StatusMessage = "Connected to Google Photos"
            });
        }
        catch (TokenResponseException ex)
        {
            logger.LogError(ex, "Token exchange failed for provider {ProviderId}", providerId);
            return TypedResults.BadRequest(new ApiError(
                "TOKEN_EXCHANGE_FAILED",
                $"Failed to exchange authorization code: {ex.Error?.Error ?? ex.Message}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth callback failed for provider {ProviderId}", providerId);
            return TypedResults.BadRequest(new ApiError(
                "OAUTH_ERROR",
                $"OAuth callback failed: {ex.Message}"));
        }
    }

    private static async Task RevokeTokenAsync(string? token, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(
                $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(token)}",
                null,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully revoked Google OAuth token");
            }
            else
            {
                logger.LogWarning(
                    "Failed to revoke Google OAuth token. Status: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error revoking Google OAuth token");
        }
    }
}

public sealed class GooglePhotosCallbackRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    public string AuthorizationCode { get; init; } = string.Empty;
}
