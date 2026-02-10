using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Update an album.
/// </summary>
public sealed class UpdateAlbumEndpoint : Endpoint<UpdateAlbumRequest, Results<Ok<AlbumDto>, NotFound>>
{
    public override void Configure()
    {
        Put("/api/admin/albums/{id:long}");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Update an album";
        });
    }

    public override async Task<Results<Ok<AlbumDto>, NotFound>> ExecuteAsync(UpdateAlbumRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, albumService, ct);
    }

    internal static async Task<Results<Ok<AlbumDto>, NotFound>> HandleRequestAsync(
        long id,
        UpdateAlbumRequest request,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var album = await albumService.UpdateAlbumAsync(id, request, ct);
        if (album is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(album);
    }
}
