using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Picker;

/// <summary>
/// Delete a Google Photos picker session.
/// </summary>
public sealed class DeleteGooglePhotosPickerSessionEndpoint : Endpoint<DeleteGooglePhotosPickerSessionRequest, Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Delete("/api/storage/google-photos/{providerId:long}/picker/sessions/{sessionId}");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Delete a picker session";
        });
    }

    public override async Task<Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        DeleteGooglePhotosPickerSessionRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req.SessionId, dbContext, pickerService, loggerFactory, ct);
    }

    internal static async Task<Results<Ok, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
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
}

public sealed class DeleteGooglePhotosPickerSessionRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}
