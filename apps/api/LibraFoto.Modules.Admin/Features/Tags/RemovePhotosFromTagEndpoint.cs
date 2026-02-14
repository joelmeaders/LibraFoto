using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Remove photos from a tag.
/// </summary>
public sealed class RemovePhotosFromTagEndpoint : Endpoint<RemovePhotosFromTagRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Delete("/api/admin/tags/{id:long}/photos");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Remove photos from a tag";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(RemovePhotosFromTagRequest req, CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, tagService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long id,
        RemovePhotosFromTagRequest request,
        ITagService tagService,
        CancellationToken ct)
    {
        var result = await tagService.RemovePhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
