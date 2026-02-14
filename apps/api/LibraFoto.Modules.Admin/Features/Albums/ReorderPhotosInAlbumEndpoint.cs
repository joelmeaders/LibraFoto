using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Reorder photos in an album.
/// </summary>
public sealed class ReorderPhotosInAlbumEndpoint : Endpoint<ReorderPhotosRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Put("/api/admin/albums/{id:long}/photos/reorder");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Reorder photos in an album";
        });
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(ReorderPhotosRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, albumService, ct);
    }

    internal static async Task<Results<NoContent, NotFound>> HandleRequestAsync(
        long id,
        ReorderPhotosRequest request,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var success = await albumService.ReorderPhotosAsync(id, request.PhotoOrders, ct);
        if (!success)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
