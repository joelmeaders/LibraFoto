using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Picker;

/// <summary>
/// Get picked media items.
/// </summary>
public sealed class GetGooglePhotosPickerItemsEndpoint : Endpoint<GetGooglePhotosPickerItemsRequest, Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Get("/api/storage/google-photos/{providerId:long}/picker/sessions/{sessionId}/items");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Get picked media items";
        });
    }

    public override async Task<Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        GetGooglePhotosPickerItemsRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req.SessionId, dbContext, pickerService, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<PickedMediaItemDto[]>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long providerId,
        string sessionId,
        LibraFotoDbContext dbContext,
        GooglePhotosPickerService pickerService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await dbContext.StorageProviders.FindAsync([providerId], cancellationToken);

        if (provider == null || provider.Type != StorageProviderType.GooglePhotos)
        {
            return TypedResults.NotFound(new ApiError(ProviderNotFoundCode, ProviderNotFoundMessage));
        }

        var config = GooglePhotosPickerHelper.ParseConfig(provider.Configuration);
        if (!GooglePhotosPickerHelper.TryGetOAuthCredentials(config, out var clientId, out var clientSecret, out var refreshToken, out var error))
        {
            return TypedResults.BadRequest(new ApiError(MissingCredentialsCode, error));
        }

        var accessToken = await GooglePhotosPickerHelper.EnsureAccessTokenAsync(config!, clientId!, clientSecret!, refreshToken!, logger, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return TypedResults.BadRequest(new ApiError(OAuthFailedCode, OAuthFailedMessage));
        }

        await GooglePhotosPickerHelper.PersistConfigAsync(provider, config!, dbContext, cancellationToken);

        var items = await pickerService.ListMediaItemsAsync(sessionId, accessToken, cancellationToken);
        var result = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => GooglePhotosPickerHelper.MapItemDto(providerId, sessionId, item))
            .ToArray();

        return TypedResults.Ok(result);
    }
}

public sealed class GetGooglePhotosPickerItemsRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}
