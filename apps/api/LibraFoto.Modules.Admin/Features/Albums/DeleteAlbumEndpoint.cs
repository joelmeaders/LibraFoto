using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Delete an album.
/// </summary>
public sealed class DeleteAlbumEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("/api/admin/albums/{id:long}");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Delete an album";
        });
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, albumService, ct);
    }

    internal static async Task<Results<NoContent, NotFound>> HandleRequestAsync(
        long id,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var deleted = await albumService.DeleteAlbumAsync(id, ct);
        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
