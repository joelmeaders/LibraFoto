using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Create a new album.
/// </summary>
public sealed class CreateAlbumEndpoint : Endpoint<CreateAlbumRequest, Created<AlbumDto>>
{
    public override void Configure()
    {
        Post("/api/admin/albums");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Create a new album";
        });
    }

    public override async Task<Created<AlbumDto>> ExecuteAsync(CreateAlbumRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        return await HandleRequestAsync(req, albumService, ct);
    }

    internal static async Task<Created<AlbumDto>> HandleRequestAsync(
        CreateAlbumRequest request,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var album = await albumService.CreateAlbumAsync(request, ct);
        return TypedResults.Created($"/api/admin/albums/{album.Id}", album);
    }
}
