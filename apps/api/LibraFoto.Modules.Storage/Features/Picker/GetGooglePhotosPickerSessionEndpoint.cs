using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Features.Shared;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Modules.Storage.Services.Repositories;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Picker;

/// <summary>
/// Get Google Photos picker session status.
/// </summary>
public sealed class GetGooglePhotosPickerSessionEndpoint : Endpoint<GetGooglePhotosPickerSessionRequest, Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Get("/api/storage/google-photos/{providerId:long}/picker/sessions/{sessionId}");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Get picker session status";
        });
    }

    public override async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        GetGooglePhotosPickerSessionRequest req,
        CancellationToken ct)
    {
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req.SessionId, storageRepository, pickerService, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long providerId,
        string sessionId,
        IStoragePersistenceRepository storageRepository,
        GooglePhotosPickerService pickerService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await storageRepository.GetProviderByIdAsync(providerId, cancellationToken);

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

        await GooglePhotosPickerHelper.PersistConfigAsync(provider, config!, storageRepository, cancellationToken);

        var session = await pickerService.GetSessionAsync(sessionId, accessToken, cancellationToken);

        var entity = await storageRepository.GetPickerSessionAsync(providerId, sessionId, cancellationToken);

        if (entity != null)
        {
            entity.MediaItemsSet = session.MediaItemsSet;
            entity.ExpiresAt = session.ExpireTime;
            await storageRepository.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(GooglePhotosPickerHelper.MapSessionDto(session));
    }
}

public sealed class GetGooglePhotosPickerSessionRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}
