using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.Configuration;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace LibraFoto.Modules.Storage.Features.Picker;

/// <summary>
/// Import picked media items into LibraFoto.
/// </summary>
public sealed class ImportGooglePhotosPickerItemsEndpoint : Endpoint<ImportGooglePhotosPickerItemsRequest, Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>>
{
    private const string LoggerCategory = "GooglePhotosPicker";
    private const string ProviderNotFoundCode = "PROVIDER_NOT_FOUND";
    private const string ProviderNotFoundMessage = "Google Photos provider not found";
    private const string MissingCredentialsCode = "MISSING_CREDENTIALS";
    private const string OAuthFailedCode = "OAUTH_FAILED";
    private const string OAuthFailedMessage = "Failed to refresh Google access token.";

    public override void Configure()
    {
        Post("/api/storage/google-photos/{providerId:long}/picker/sessions/{sessionId}/import");
        Tags("Storage - Google Photos Picker");
        Summary(s =>
        {
            s.Summary = "Import picked media items into LibraFoto";
        });
    }

    public override async Task<Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>> ExecuteAsync(
        ImportGooglePhotosPickerItemsRequest req,
        CancellationToken ct)
    {
        var dbContext = Resolve<LibraFotoDbContext>();
        var pickerService = Resolve<GooglePhotosPickerService>();
        var imageImport = Resolve<IImageImportService>();
        var configuration = Resolve<IConfiguration>();
        var loggerFactory = Resolve<ILoggerFactory>();
        return await HandleRequestAsync(req.ProviderId, req.SessionId, dbContext, pickerService, imageImport, configuration, loggerFactory, ct);
    }

    internal static async Task<Results<Ok<ImportPickerItemsResponse>, NotFound<ApiError>, BadRequest<ApiError>>> HandleRequestAsync(
        long providerId,
        string sessionId,
        LibraFotoDbContext dbContext,
        GooglePhotosPickerService pickerService,
        IImageImportService imageImport,
        IConfiguration configuration,
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
        var imported = 0;
        var failed = 0;

        var importContext = new PickerImportContext(
            providerId,
            sessionId,
            accessToken,
            pickerService,
            imageImport,
            configuration,
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

        var isVideo = string.Equals(item.Type, "VIDEO", StringComparison.OrdinalIgnoreCase);
        var isImage = !isVideo;

        var extension = !string.IsNullOrWhiteSpace(item.MediaFile.Filename)
            ? Path.GetExtension(item.MediaFile.Filename).ToLowerInvariant()
            : GooglePhotosPickerHelper.GetExtensionFromMimeType(item.MediaFile.MimeType);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".jpg";
        }

        var storagePath = context.Configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
        var maxDimension = context.Configuration.GetValue("Storage:MaxImportDimension", 2560);
        var dateTaken = item.CreateTime ?? DateTime.UtcNow;
        var yearMonth = Path.Combine(dateTaken.Year.ToString(), dateTaken.Month.ToString("D2"));

        Photo? photo = null;
        string? uploadedFilePath = null;
        string? thumbnailFilePath = null;

        try
        {
            var existingPhoto = await context.DbContext.Photos
                .FirstOrDefaultAsync(p => p.ProviderId == context.ProviderId && p.ProviderFileId == item.Id, cancellationToken);

            var fileName = item.MediaFile.Filename ?? item.Id;
            var width = item.MediaFile.MediaFileMetadata?.Width ?? 0;
            var height = item.MediaFile.MediaFileMetadata?.Height ?? 0;

            var (downloadStream, _) = await context.PickerService.DownloadMediaItemAsync(
                item.MediaFile.BaseUrl,
                context.AccessToken,
                isVideo,
                maxWidth: 4096,
                maxHeight: 4096,
                cancellationToken);

            await using var tempStream = downloadStream;

            if (existingPhoto != null)
            {
                photo = existingPhoto;
            }
            else
            {
                photo = new Photo
                {
                    Filename = fileName,
                    OriginalFilename = fileName,
                    FilePath = "",
                    FileSize = 0,
                    Width = width,
                    Height = height,
                    MediaType = isImage ? MediaType.Photo : MediaType.Video,
                    Duration = null,
                    DateTaken = dateTaken,
                    DateAdded = DateTime.UtcNow,
                    ProviderId = context.ProviderId,
                    ProviderFileId = item.Id
                };

                context.DbContext.Photos.Add(photo);
                await context.DbContext.SaveChangesAsync(cancellationToken);
            }

            var idFilename = $"{photo.Id}{extension}";
            var relativePath = Path.Combine("media", yearMonth, idFilename).Replace('\\', '/');
            uploadedFilePath = Path.Combine(storagePath, "media", yearMonth, idFilename);
            Directory.CreateDirectory(Path.GetDirectoryName(uploadedFilePath)!);

            if (isImage)
            {
                var importResult = await context.ImageImport.ProcessImageAsync(
                    tempStream, uploadedFilePath, maxDimension, cancellationToken);

                if (!importResult.Success)
                {
                    throw new InvalidOperationException(importResult.ErrorMessage ?? "Image processing failed");
                }

                photo.Width = importResult.Width;
                photo.Height = importResult.Height;
                photo.FileSize = importResult.FileSize;
            }
            else
            {
                await using var fileStream = new FileStream(
                    uploadedFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await tempStream.CopyToAsync(fileStream, cancellationToken);

                photo.FileSize = new FileInfo(uploadedFilePath).Length;
            }

            if (isImage)
            {
                var thumbnailBasePath = Path.Combine(storagePath, ".thumbnails", yearMonth);
                Directory.CreateDirectory(thumbnailBasePath);

                try
                {
                    using var sourceImage = await Image.LoadAsync(uploadedFilePath, cancellationToken);
                    sourceImage.Mutate(ctx => ctx.AutoOrient());

                    using var thumbnail = sourceImage.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(400, 400),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));

                    thumbnailFilePath = Path.Combine(thumbnailBasePath, $"{photo.Id}.jpg");
                    var encoder = new JpegEncoder { Quality = 85 };
                    await thumbnail.SaveAsync(thumbnailFilePath, encoder, cancellationToken);

                    var relativeThumbnailPath = Path.Combine(".thumbnails", yearMonth, $"{photo.Id}.jpg").Replace('\\', '/');
                    photo.ThumbnailPath = relativeThumbnailPath;
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Failed to generate thumbnail for picker item {ItemId}", item.Id);
                }
            }

            photo.Filename = idFilename;
            photo.FilePath = relativePath;
            photo.ProviderFileId = item.Id;
            photo.ProviderId = context.ProviderId;

            await context.DbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to import picker item {ItemId}, cleaning up", item.Id);

            if (!string.IsNullOrEmpty(uploadedFilePath) && File.Exists(uploadedFilePath))
            {
                try
                { File.Delete(uploadedFilePath); }
                catch (Exception deleteEx)
                {
                    context.Logger.LogWarning(deleteEx, "Failed to delete file {Path} during cleanup", uploadedFilePath);
                }
            }

            if (!string.IsNullOrEmpty(thumbnailFilePath) && File.Exists(thumbnailFilePath))
            {
                try
                { File.Delete(thumbnailFilePath); }
                catch (Exception deleteEx)
                {
                    context.Logger.LogWarning(deleteEx, "Failed to delete thumbnail {Path} during cleanup", thumbnailFilePath);
                }
            }

            if (photo != null && photo.Id > 0)
            {
                try
                {
                    if (string.IsNullOrEmpty(photo.FilePath))
                    {
                        context.DbContext.Photos.Remove(photo);
                        await context.DbContext.SaveChangesAsync(CancellationToken.None);
                    }
                }
                catch (Exception deleteEx)
                {
                    context.Logger.LogWarning(deleteEx, "Failed to remove Photo record {PhotoId} during cleanup", photo.Id);
                }
            }

            return false;
        }
    }
}

public sealed class ImportGooglePhotosPickerItemsRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("sessionId")]
    public string SessionId { get; init; } = string.Empty;
}

public record ImportPickerItemsResponse
{
    public int Imported { get; init; }
    public int Failed { get; init; }
}

internal record PickerImportContext(
    long ProviderId,
    string SessionId,
    string AccessToken,
    GooglePhotosPickerService PickerService,
    IImageImportService ImageImport,
    IConfiguration Configuration,
    LibraFotoDbContext DbContext,
    ILogger Logger);
