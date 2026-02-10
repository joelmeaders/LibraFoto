using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Remove photos from an album.
/// </summary>
public sealed class RemovePhotosFromAlbumEndpoint : Endpoint<RemovePhotosFromAlbumRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Delete("/api/admin/albums/{id:long}/photos");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Remove photos from an album";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(RemovePhotosFromAlbumRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, albumService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long id,
        RemovePhotosFromAlbumRequest request,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var result = await albumService.RemovePhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
