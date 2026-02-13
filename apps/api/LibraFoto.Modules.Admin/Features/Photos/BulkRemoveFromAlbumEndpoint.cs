using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Remove multiple photos from an album.
/// </summary>
public sealed class BulkRemoveFromAlbumEndpoint : Endpoint<RemovePhotosFromAlbumRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/photos/bulk/remove-from-album/{albumId:long}");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Remove multiple photos from an album";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(RemovePhotosFromAlbumRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        var albumId = Route<long>("albumId");
        return await HandleRequestAsync(albumId, req, photoService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long albumId,
        RemovePhotosFromAlbumRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var result = await photoService.RemovePhotosFromAlbumAsync(albumId, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
