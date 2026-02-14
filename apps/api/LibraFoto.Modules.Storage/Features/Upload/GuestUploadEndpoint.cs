using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Modules.Storage.Services.Repositories;
using LibraFoto.Modules.Storage.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Upload;

/// <summary>
/// Upload files via guest link.
/// </summary>
public sealed class GuestUploadEndpoint : Endpoint<GuestUploadRequest, Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>>
{
    public override void Configure()
    {
        Post("/api/guest/upload/{linkId}");
        AllowAnonymous();
        AllowFileUploads();
        Tags("Guest Upload");
        Summary(s =>
        {
            s.Summary = "Upload via guest link";
            s.Description = "Uploads files using a guest link for authentication.";
        });
    }

    public override async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>> ExecuteAsync(
        GuestUploadRequest req,
        CancellationToken ct)
    {
        var providerFactory = Resolve<IStorageProviderFactory>();
        var mediaScanner = Resolve<IMediaScannerService>();
        var imageImport = Resolve<IImageImportService>();
        var configuration = Resolve<IConfiguration>();
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var logger = Resolve<ILogger<object>>();
        var files = Files?.ToList() ?? [];

        return await HandleRequestAsync(
            req.LinkId,
            files,
            providerFactory,
            mediaScanner,
            imageImport,
            configuration,
                storageRepository,
            logger,
            ct);
    }

    internal static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>, NotFound<ApiError>, StatusCodeHttpResult>> HandleRequestAsync(
        string linkId,
        IReadOnlyCollection<IFormFile> files,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        IImageImportService imageImport,
        IConfiguration configuration,
        IStoragePersistenceRepository storageRepository,
        ILogger<object> logger,
        CancellationToken cancellationToken)
    {
        var guestLink = await storageRepository.GetGuestLinkByIdAsync(linkId, cancellationToken);

        if (guestLink == null)
        {
            return TypedResults.NotFound(new ApiError("LINK_NOT_FOUND", "Guest link not found"));
        }

        if (guestLink.ExpiresAt.HasValue && guestLink.ExpiresAt.Value < DateTime.UtcNow)
        {
            return TypedResults.BadRequest(new ApiError("LINK_EXPIRED", "Guest link has expired"));
        }

        if (guestLink.MaxUploads.HasValue && guestLink.CurrentUploads >= guestLink.MaxUploads.Value)
        {
            return TypedResults.BadRequest(new ApiError("LINK_EXHAUSTED", "Guest link has reached maximum uploads"));
        }

        if (files == null || files.Count == 0)
        {
            return TypedResults.BadRequest(new ApiError("NO_FILES", "No files were provided"));
        }

        var results = new List<UploadResult>();
        var successful = 0;
        var failed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length == 0 || file.Length > UploadHelpers.MaxFileSize || !mediaScanner.IsSupportedMediaFile(file.FileName))
            {
                results.Add(UploadResult.Failed("Invalid file"));
                failed++;
                continue;
            }

            var result = await UploadHelpers.ProcessSingleUploadAsync(
                file,
                guestLink.TargetAlbumId,
                providerFactory,
                mediaScanner,
                imageImport,
                configuration,
                storageRepository,
                logger,
                cancellationToken);

            results.Add(result);
            if (result.Success)
            {
                successful++;
            }
            else
            {
                failed++;
            }
        }

        if (successful > 0)
        {
            var linkToUpdate = await storageRepository.GetGuestLinkByIdAsync(guestLink.Id, cancellationToken);
            if (linkToUpdate != null)
            {
                linkToUpdate.CurrentUploads += successful;
                await storageRepository.SaveChangesAsync(cancellationToken);
            }
        }

        return TypedResults.Ok(new BatchUploadResult
        {
            TotalFiles = files.Count,
            SuccessfulUploads = successful,
            FailedUploads = failed,
            Results = results
        });
    }
}

public sealed class GuestUploadRequest
{
    [BindFrom("linkId")]
    public string LinkId { get; init; } = string.Empty;
}
