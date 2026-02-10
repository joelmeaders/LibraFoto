using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Get an album by ID.
/// </summary>
public sealed class GetAlbumByIdEndpoint : Endpoint<GetAlbumByIdRequest, Results<Ok<AlbumDto>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/admin/albums/{id:long}");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Get an album by ID";
        });
    }

    public override async Task<Results<Ok<AlbumDto>, NotFound>> ExecuteAsync(GetAlbumByIdRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        return await HandleRequestAsync(req.Id, albumService, ct);
    }

    internal static async Task<Results<Ok<AlbumDto>, NotFound>> HandleRequestAsync(
        long id,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var album = await albumService.GetAlbumByIdAsync(id, ct);
        if (album is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(album);
    }
}

public sealed class GetAlbumByIdRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
