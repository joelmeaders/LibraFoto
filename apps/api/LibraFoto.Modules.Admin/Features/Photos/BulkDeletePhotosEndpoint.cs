using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Delete multiple photos.
/// </summary>
public sealed class BulkDeletePhotosEndpoint : Endpoint<BulkPhotoRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/photos/bulk/delete");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Delete multiple photos";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(BulkPhotoRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(req, photoService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        BulkPhotoRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var result = await photoService.DeletePhotosAsync(request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
