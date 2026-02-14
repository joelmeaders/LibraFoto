using FastEndpoints;
using LibraFoto.Data.Entities;
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
/// Start a Google Photos picker session.
/// </summary>
public sealed class StartGooglePhotosPickerSessionEndpoint : Endpoint<StartPickerSessionRequest, Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Post("/api/storage/google-photos/{providerId:long}/picker/start");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Start a Google Photos picker session";
        });
    }

    public override async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        StartPickerSessionRequest req,
        CancellationToken ct)
    {
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req, storageRepository, pickerService, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<PickerSessionDto>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long providerId,
        StartPickerSessionRequest request,
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

        var session = await pickerService.CreateSessionAsync(accessToken, request.MaxItemCount, cancellationToken);
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

        await storageRepository.AddPickerSessionAsync(entity, cancellationToken);
        await storageRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(GooglePhotosPickerHelper.MapSessionDto(session));
    }
}

public sealed class StartPickerSessionRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    public long? MaxItemCount { get; init; }
}
