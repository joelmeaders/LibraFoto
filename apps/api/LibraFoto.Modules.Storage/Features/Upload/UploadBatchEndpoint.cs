using FastEndpoints;
using LibraFoto.Data;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using LibraFoto.Modules.Storage.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Features.Upload;

/// <summary>
/// Upload multiple files.
/// </summary>
public sealed class UploadBatchEndpoint : Endpoint<UploadBatchRequest, Results<Ok<BatchUploadResult>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/admin/upload/batch");
        AllowFileUploads();
        Tags("Upload");
        Summary(s =>
        {
            s.Summary = "Upload multiple files";
            s.Description = "Uploads multiple photo or video files to local storage.";
        });
        // TODO: Re-enable authorization for /api/admin/upload when auth enforcement is ready.
    }

    public override async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>>> ExecuteAsync(
        UploadBatchRequest req,
        CancellationToken ct)
    {
        var providerFactory = Resolve<IStorageProviderFactory>();
        var mediaScanner = Resolve<IMediaScannerService>();
        var imageImport = Resolve<IImageImportService>();
        var configuration = Resolve<IConfiguration>();
        var dbContext = Resolve<LibraFotoDbContext>();
        var logger = Resolve<ILogger<object>>();
        var files = Files?.ToList() ?? [];

        return await HandleRequestAsync(
            files,
            req.AlbumId,
            providerFactory,
            mediaScanner,
            imageImport,
            configuration,
            dbContext,
            logger,
            ct);
    }

    internal static async Task<Results<Ok<BatchUploadResult>, BadRequest<ApiError>>> HandleRequestAsync(
        IReadOnlyCollection<IFormFile> files,
        long? albumId,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        IImageImportService imageImport,
        IConfiguration configuration,
        LibraFotoDbContext dbContext,
        ILogger<object> logger,
        CancellationToken cancellationToken)
    {
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

            if (file.Length == 0)
            {
                results.Add(UploadResult.Failed("Empty file"));
                failed++;
                continue;
            }

            if (file.Length > UploadHelpers.MaxFileSize)
            {
                results.Add(UploadResult.Failed($"File exceeds maximum size of {UploadHelpers.MaxFileSize / 1024 / 1024} MB"));
                failed++;
                continue;
            }

            if (!mediaScanner.IsSupportedMediaFile(file.FileName))
            {
                results.Add(UploadResult.Failed($"Unsupported file type: {Path.GetExtension(file.FileName)}"));
                failed++;
                continue;
            }

            var result = await UploadHelpers.ProcessSingleUploadAsync(
                file,
                albumId,
                providerFactory,
                mediaScanner,
                imageImport,
                configuration,
                dbContext,
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

        return TypedResults.Ok(new BatchUploadResult
        {
            TotalFiles = files.Count,
            SuccessfulUploads = successful,
            FailedUploads = failed,
            Results = results
        });
    }
}

public sealed class UploadBatchRequest
{
    [QueryParam]
    public long? AlbumId { get; init; }
}
