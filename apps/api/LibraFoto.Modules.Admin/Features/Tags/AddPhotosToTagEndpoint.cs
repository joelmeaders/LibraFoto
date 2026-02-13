using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Add photos to a tag.
/// </summary>
public sealed class AddPhotosToTagEndpoint : Endpoint<AddPhotosToTagRequest, Ok<BulkOperationResult>>
{
    public override void Configure()
    {
        Post("/api/admin/tags/{id:long}/photos");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Add photos to a tag";
        });
    }

    public override async Task<Ok<BulkOperationResult>> ExecuteAsync(AddPhotosToTagRequest req, CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, tagService, ct);
    }

    internal static async Task<Ok<BulkOperationResult>> HandleRequestAsync(
        long id,
        AddPhotosToTagRequest request,
        ITagService tagService,
        CancellationToken ct)
    {
        var result = await tagService.AddPhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
