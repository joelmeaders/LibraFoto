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
/// Upload a single file.
/// </summary>
public sealed class UploadFileEndpoint : Endpoint<UploadFileRequest, Results<Ok<UploadResult>, BadRequest<ApiError>>>
{
    public override void Configure()
    {
        Post("/api/admin/upload");
        AllowFileUploads();
        Tags("Upload");
        Summary(s =>
        {
            s.Summary = "Upload a file";
            s.Description = "Uploads a photo or video file to local storage.";
        });
        // TODO: Re-enable authorization for /api/admin/upload when auth enforcement is ready.
    }

    public override async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> ExecuteAsync(
        UploadFileRequest req,
        CancellationToken ct)
    {
        var providerFactory = Resolve<IStorageProviderFactory>();
        var mediaScanner = Resolve<IMediaScannerService>();
        var imageImport = Resolve<IImageImportService>();
        var configuration = Resolve<IConfiguration>();
        var storageRepository = Resolve<IStoragePersistenceRepository>();
        var logger = Resolve<ILogger<object>>();
        var file = req.File ?? Files.FirstOrDefault();

        return await HandleRequestAsync(
            file,
            req.AlbumId,
            providerFactory,
            mediaScanner,
            imageImport,
            configuration,
                storageRepository,
            logger,
            ct);
    }

    internal static async Task<Results<Ok<UploadResult>, BadRequest<ApiError>>> HandleRequestAsync(
        IFormFile? file,
        long? albumId,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        IImageImportService imageImport,
        IConfiguration configuration,
        IStoragePersistenceRepository storageRepository,
        ILogger<object> logger,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return TypedResults.BadRequest(new ApiError("NO_FILE", "No file was provided"));
        }

        if (file.Length > UploadHelpers.MaxFileSize)
        {
            return TypedResults.BadRequest(new ApiError("FILE_TOO_LARGE", $"File exceeds maximum size of {UploadHelpers.MaxFileSize / 1024 / 1024} MB"));
        }

        if (!mediaScanner.IsSupportedMediaFile(file.FileName))
        {
            return TypedResults.BadRequest(new ApiError("UNSUPPORTED_TYPE", $"File type not supported: {Path.GetExtension(file.FileName)}"));
        }

        var result = await UploadHelpers.ProcessSingleUploadAsync(
            file,
            albumId,
            providerFactory,
            mediaScanner,
            imageImport,
            configuration,
            storageRepository,
            logger,
            cancellationToken);

        if (!result.Success)
        {
            return TypedResults.BadRequest(new ApiError("UPLOAD_FAILED", result.ErrorMessage ?? "Upload failed"));
        }

        return TypedResults.Ok(result);
    }
}

public sealed class UploadFileRequest
{
    public IFormFile? File { get; init; }

    [QueryParam]
    public long? AlbumId { get; init; }
}
