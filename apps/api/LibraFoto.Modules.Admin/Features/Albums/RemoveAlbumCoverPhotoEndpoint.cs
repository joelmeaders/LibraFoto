using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Remove the cover photo from an album.
/// </summary>
public sealed class RemoveAlbumCoverPhotoEndpoint : EndpointWithoutRequest<Results<Ok<AlbumDto>, NotFound>>
{
    public override void Configure()
    {
        Delete("/api/admin/albums/{id:long}/cover");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Remove the cover photo from an album";
        });
    }

    public override async Task<Results<Ok<AlbumDto>, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, albumService, ct);
    }

    internal static async Task<Results<Ok<AlbumDto>, NotFound>> HandleRequestAsync(
        long id,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var album = await albumService.RemoveCoverPhotoAsync(id, ct);
        if (album is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(album);
    }
}
