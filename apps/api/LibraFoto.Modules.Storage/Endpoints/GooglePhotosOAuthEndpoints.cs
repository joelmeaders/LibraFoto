using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Endpoints
{
    /// <summary>
    /// Endpoints for Google Photos OAuth integration.
    /// </summary>
    public static class GooglePhotosOAuthEndpoints
    {
        private static readonly string[] _googlePhotosScopes =
        [
            "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
        ];

        /// <summary>
        /// Maps Google Photos OAuth endpoints.
        /// </summary>
        public static IEndpointRouteBuilder MapGooglePhotosOAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/storage/google-photos")
                .WithTags("Storage - Google Photos OAuth");
            // Note: Not requiring authorization for OAuth flow endpoints

            group.MapGet("/{providerId:long}/authorize-url", GetAuthorizationUrl)
                .WithName("GetGooglePhotosAuthUrl")
                .WithSummary("Get the OAuth authorization URL for Google Photos");

            group.MapPost("/{providerId:long}/callback", HandleOAuthCallback)
                .WithName("GooglePhotosOAuthCallback")
                .WithSummary("Handle OAuth callback from Google");

            return app;
        }

        /// <summary>
        /// Generates the Google OAuth authorization URL.
        /// </summary>
        private static async Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>> GetAuthorizationUrl(
            long providerId,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

            if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
            {
                return TypedResults.NotFound(new ApiError("PROVIDER_NOT_FOUND", "Google Photos provider not found"));
            }

            // Try to get client credentials from configuration
            GooglePhotosConfiguration? config = null;
            if (!string.IsNullOrEmpty(provider.Configuration))
            {
                config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(provider.Configuration);
            }

            // Use environment variables if not in config
            var clientId = config?.ClientId ?? configuration["GooglePhotos:ClientId"];
            var clientSecret = config?.ClientSecret ?? configuration["GooglePhotos:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return TypedResults.NotFound(new ApiError(
                    "MISSING_CREDENTIALS",
                    "Google Photos client ID and secret must be configured"));
            }

            var redirectUri = configuration["GooglePhotos:RedirectUri"] ?? $"http://localhost:4200/oauth/callback";

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = _googlePhotosScopes
            });

            var authUrl = flow.CreateAuthorizationCodeRequest(redirectUri);
            authUrl.State = providerId.ToString(); // Pass provider ID in state parameter
            authUrl.Scope = string.Join(" ", _googlePhotosScopes);
            if (authUrl is GoogleAuthorizationCodeRequestUrl googleAuthUrl)
            {
                googleAuthUrl.AccessType = "offline";
                googleAuthUrl.Prompt = "consent";
                googleAuthUrl.IncludeGrantedScopes = "true";
            }
            var url = authUrl.Build().ToString();

            return TypedResults.Ok(new GooglePhotosAuthUrlResponse
            {
                AuthorizationUrl = url,
                RedirectUri = redirectUri
            });
        }

        /// <summary>
        /// Handles the OAuth callback from Google.
        /// </summary>
        private static async Task<Results<Ok<StorageProviderDto>, BadRequest<ApiError>, NotFound<ApiError>>> HandleOAuthCallback(
            long providerId,
            [FromBody] GooglePhotosCallbackRequest request,
            [FromServices] LibraFotoDbContext dbContext,
            [FromServices] IConfiguration configuration,
            [FromServices] ILoggerFactory loggerFactory,
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

                // Get client credentials from provider config or environment
                GooglePhotosConfiguration? existingConfig = null;
                if (!string.IsNullOrEmpty(provider.Configuration))
                {
                    existingConfig = JsonSerializer.Deserialize<GooglePhotosConfiguration>(provider.Configuration);
                }

                var clientId = existingConfig?.ClientId ?? configuration["GooglePhotos:ClientId"];
                var clientSecret = existingConfig?.ClientSecret ?? configuration["GooglePhotos:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    return TypedResults.BadRequest(new ApiError(
                        "MISSING_CREDENTIALS",
                        "Google Photos client ID and secret must be configured"));
                }

                var redirectUri = configuration["GooglePhotos:RedirectUri"] ?? $"http://localhost:4200/oauth/callback";

                // Exchange authorization code for tokens
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
                    request.AuthorizationCode,
                    redirectUri,
                    cancellationToken);

                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    return TypedResults.BadRequest(new ApiError(
                        "OAUTH_FAILED",
                        "Failed to obtain refresh token from Google. Please ensure your app is configured for offline access."));
                }

                // Log what Google returned for debugging
                logger.LogInformation(
                    "Google OAuth token response - Scope: [{Scope}], HasRefreshToken: {HasRefresh}, ExpiresIn: {ExpiresIn}",
                    tokenResponse.Scope ?? "(null)",
                    !string.IsNullOrEmpty(tokenResponse.RefreshToken),
                    tokenResponse.ExpiresInSeconds);

                // Validate that Google granted the required scopes
                var grantedScopes = (tokenResponse.Scope ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                logger.LogDebug(
                    "Parsed granted scopes: [{Scopes}], Count: {Count}",
                    string.Join(", ", grantedScopes),
                    grantedScopes.Count);

                // If no scopes were returned, that's a problem
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

                    // Revoke the partially-granted token since it's unusable
                    await RevokeTokenAsync(tokenResponse.AccessToken, logger, cancellationToken);

                    return TypedResults.BadRequest(new ApiError(
                        "INSUFFICIENT_SCOPES",
                        $"Google did not grant all required permissions. Missing scopes: {string.Join(", ", missingScopes)}. " +
                        "This can happen if your Google Cloud app is not verified. Go to your Google Account settings " +
                        "(https://myaccount.google.com/permissions), remove access for this app, then try connecting again " +
                        "and ensure you approve ALL requested permissions."));
                }

                // Create/update configuration with tokens
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

                // Update provider configuration
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

        /// <summary>
        /// Revokes an OAuth token with Google.
        /// </summary>
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

    /// <summary>
    /// Response containing Google Photos authorization URL.
    /// </summary>
    public record GooglePhotosAuthUrlResponse
    {
        public required string AuthorizationUrl { get; init; }
        public required string RedirectUri { get; init; }
    }

    /// <summary>
    /// Request to handle OAuth callback from Google.
    /// </summary>
    public record GooglePhotosCallbackRequest
    {
        public required string AuthorizationCode { get; init; }
    }
}
