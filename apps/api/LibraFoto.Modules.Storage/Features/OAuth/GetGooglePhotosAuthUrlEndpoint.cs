using System.Text.Json;
using FastEndpoints;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace LibraFoto.Modules.Storage.Features.OAuth;

/// <summary>
/// Get Google Photos OAuth authorization URL.
/// </summary>
public sealed class GetGooglePhotosAuthUrlEndpoint : Endpoint<GetGooglePhotosAuthUrlRequest, Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>>
{
    private static readonly string[] _googlePhotosScopes =
    [
        "https://www.googleapis.com/auth/photospicker.mediaitems.readonly"
    ];

    public override void Configure()
    {
        Get("/api/storage/google-photos/{providerId:long}/authorize-url");
        Tags("Storage - Google Photos OAuth");
        Summary(s =>
        {
            s.Summary = "Get the OAuth authorization URL for Google Photos";
        });
    }

    public override async Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>> ExecuteAsync(
        GetGooglePhotosAuthUrlRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var configuration = Resolve<IConfiguration>();
        return await HandleRequestAsync(req.ProviderId, dbContext, configuration, ct);
    }

    internal static async Task<Results<Ok<GooglePhotosAuthUrlResponse>, NotFound<ApiError>>> HandleRequestAsync(
        long providerId,
        LibraFotoDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError("PROVIDER_NOT_FOUND", "Google Photos provider not found"));
        }

        GooglePhotosConfiguration? config = null;
        if (!string.IsNullOrEmpty(provider.Configuration))
        {
            try
            {
                config = JsonSerializer.Deserialize<GooglePhotosConfiguration>(provider.Configuration);
            }
            catch (JsonException)
            {
                config = null;
            }
        }

        var clientId = config?.ClientId ?? configuration["GooglePhotos:ClientId"];
        var clientSecret = config?.ClientSecret ?? configuration["GooglePhotos:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return TypedResults.NotFound(new ApiError(
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

        var authUrl = flow.CreateAuthorizationCodeRequest(redirectUri);
        authUrl.State = providerId.ToString();
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
}

public sealed class GetGooglePhotosAuthUrlRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }
}

public record GooglePhotosAuthUrlResponse
{
    public required string AuthorizationUrl { get; init; }
    public required string RedirectUri { get; init; }
}
