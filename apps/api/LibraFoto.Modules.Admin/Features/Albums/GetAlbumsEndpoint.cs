using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// List all albums.
/// </summary>
public sealed class GetAlbumsEndpoint : EndpointWithoutRequest<Ok<IReadOnlyList<AlbumDto>>>
{
    public override void Configure()
    {
        Get("/api/admin/albums");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Get all albums";
        });
    }

    public override async Task<Ok<IReadOnlyList<AlbumDto>>> ExecuteAsync(CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        return await HandleRequestAsync(albumService, ct);
    }

    internal static async Task<Ok<IReadOnlyList<AlbumDto>>> HandleRequestAsync(IAlbumService albumService, CancellationToken ct)
    {
        var albums = await albumService.GetAlbumsAsync(ct);
        return TypedResults.Ok(albums);
    }
}
