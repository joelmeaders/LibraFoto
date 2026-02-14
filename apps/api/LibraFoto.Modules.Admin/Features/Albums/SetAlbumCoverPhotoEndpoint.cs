using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Set the cover photo for an album.
/// </summary>
public sealed class SetAlbumCoverPhotoEndpoint : EndpointWithoutRequest<Results<Ok<AlbumDto>, NotFound>>
{
    public override void Configure()
    {
        Put("/api/admin/albums/{id:long}/cover/{photoId:long}");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Set the cover photo for an album";
        });
    }

    public override async Task<Results<Ok<AlbumDto>, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        var photoId = Route<long>("photoId");
        return await HandleRequestAsync(id, photoId, albumService, ct);
    }

    internal static async Task<Results<Ok<AlbumDto>, NotFound>> HandleRequestAsync(
        long id,
        long photoId,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var album = await albumService.SetCoverPhotoAsync(id, photoId, ct);
        if (album is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(album);
    }
}
