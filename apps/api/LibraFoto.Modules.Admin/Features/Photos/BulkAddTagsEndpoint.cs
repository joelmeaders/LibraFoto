using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Photos;

/// <summary>
/// Add tags to multiple photos.
/// </summary>
public sealed class BulkAddTagsEndpoint : Endpoint<AddTagsToPhotosRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/photos/bulk/add-tags");
        Tags("Photos");
        Summary(s =>
        {
            s.Summary = "Add tags to multiple photos";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(AddTagsToPhotosRequest req, CancellationToken ct)
    {
        var photoService = Resolve<IPhotoService>();
        return await HandleRequestAsync(req, photoService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        AddTagsToPhotosRequest request,
        IPhotoService photoService,
        CancellationToken ct)
    {
        var result = await photoService.AddTagsToPhotosAsync(request.PhotoIds, request.TagIds, ct);
        return TypedResults.Ok(result);
    }
}
