using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Modules.Storage.Services.Repositories;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Picker;

/// <summary>
/// Get a picker item thumbnail.
/// </summary>
public sealed class GetGooglePhotosPickerThumbnailEndpoint : Endpoint<PickerThumbnailRequest, Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Get("/api/storage/google-photos/{providerId:long}/picker/sessions/{sessionId}/items/{itemId}/thumbnail");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Get a picker item thumbnail";
        });
    }

    public override async Task<Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        PickerThumbnailRequest req,
        CancellationToken ct)
    {
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req, storageRepository, pickerService, loggerFactory, ct);
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        PickerThumbnailRequest request,
        IStoragePersistenceRepository storageRepository,
        GooglePhotosPickerService pickerService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var provider = await storageRepository.GetProviderByIdAsync(request.ProviderId, cancellationToken);

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
}

public sealed class PickerThumbnailRequest
{
    public PickerThumbnailRequest()
    {
    }

    public PickerThumbnailRequest(long providerId, string sessionId, string itemId, int width, int height)
    {
        ProviderId = providerId;
        SessionId = sessionId;
        ItemId = itemId;
        Width = width;
        Height = height;
    }

    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [BindFrom("itemId")]
    public string ItemId { get; init; } = string.Empty;

    [QueryParam]
    public int Width { get; init; }

    [QueryParam]
    public int Height { get; init; }
}
