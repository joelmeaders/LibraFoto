using FastEndpoints;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Storage.Features.Files;

/// <summary>
/// Retrieve a file from a storage provider.
/// </summary>
public sealed class GetFileEndpoint : Endpoint<GetFileRequest, Results<FileStreamHttpResult, NotFound<ApiError>>>
{
    public override void Configure()
    {
        Get("/api/files/{providerId:long}/{**fileId}");
        Policies("Authenticated");
        Tags("Files");
        Summary(s =>
        {
            s.Summary = "Get a file";
            s.Description = "Retrieves a file from a storage provider.";
        });
    }

    public override async Task<Results<FileStreamHttpResult, NotFound<ApiError>>> ExecuteAsync(GetFileRequest req, CancellationToken ct)
    {
        var providerFactory = Resolve<IStorageProviderFactory>();
        var mediaScanner = Resolve<IMediaScannerService>();
        return await HandleRequestAsync(req.ProviderId, req.FileId, providerFactory, mediaScanner, ct);
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound<ApiError>>> HandleRequestAsync(
        long providerId,
        string fileId,
        IStorageProviderFactory providerFactory,
        IMediaScannerService mediaScanner,
        CancellationToken cancellationToken)
    {
        var provider = await providerFactory.GetProviderAsync(providerId, cancellationToken);

        if (provider == null)
        {
            return TypedResults.NotFound(new ApiError("PROVIDER_NOT_FOUND", "Storage provider not found"));
        }

        try
        {
            var stream = await provider.GetFileStreamAsync(fileId, cancellationToken);
            var contentType = mediaScanner.GetContentType(fileId);
            var fileName = Path.GetFileName(fileId);

            return TypedResults.File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return TypedResults.NotFound(new ApiError("FILE_NOT_FOUND", $"File not found: {fileId}"));
        }
    }
}

public sealed class GetFileRequest
{
    [BindFrom("providerId")]
    public long ProviderId { get; init; }

    [BindFrom("fileId")]
    public string FileId { get; init; } = string.Empty;
}
