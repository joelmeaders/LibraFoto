using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Remove tags from multiple photos.
/// </summary>
public sealed class BulkRemoveTagsEndpoint : Endpoint<RemoveTagsFromPhotosRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/photos/bulk/remove-tags");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Remove tags from multiple photos";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(RemoveTagsFromPhotosRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(req, photoService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        RemoveTagsFromPhotosRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var result = await photoService.RemoveTagsFromPhotosAsync(request.PhotoIds, request.TagIds, ct);
        return TypedResults.Ok(result);
    }
}
