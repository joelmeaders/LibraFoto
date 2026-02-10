using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Delete a single photo.
/// </summary>
public sealed class DeletePhotoEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("/api/admin/photos/{id:long}");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Delete a single photo";
        });
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, photoService, ct);
    }

    internal static async Task<Results<NoContent, NotFound>> HandleRequestAsync(
        long id,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var deleted = await photoService.DeletePhotoAsync(id, ct);
        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
