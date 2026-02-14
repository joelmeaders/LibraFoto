using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Add multiple photos to an album.
/// </summary>
public sealed class BulkAddToAlbumEndpoint : Endpoint<AddPhotosToAlbumRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/photos/bulk/add-to-album/{albumId:long}");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Add multiple photos to an album";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(AddPhotosToAlbumRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        var albumId = Route<long>("albumId");
        return await HandleRequestAsync(albumId, req, photoService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long albumId,
        AddPhotosToAlbumRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var result = await photoService.AddPhotosToAlbumAsync(albumId, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
