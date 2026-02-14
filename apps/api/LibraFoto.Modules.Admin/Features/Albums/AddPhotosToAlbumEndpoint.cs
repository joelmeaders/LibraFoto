using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Albums;

/// <summary>
/// Add photos to an album.
/// </summary>
public sealed class AddPhotosToAlbumEndpoint : Endpoint<AddPhotosToAlbumRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/albums/{id:long}/photos");
        Tags("Albums");
        Summary(s =>
        {
            s.Summary = "Add photos to an album";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(AddPhotosToAlbumRequest req, CancellationToken ct)
    {
        var albumService = Resolve<IAlbumService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, albumService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long id,
        AddPhotosToAlbumRequest request,
        IAlbumService albumService,
        CancellationToken ct)
    {
        var result = await albumService.AddPhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
