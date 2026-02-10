using FastEndpoints;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Update a tag.
/// </summary>
public sealed class UpdateTagEndpoint : Endpoint<UpdateTagRequest, Results<Ok<TagDto>, NotFound>>
{
    public override void Configure()
    {
        Put("/api/admin/tags/{id:long}");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Update a tag";
        });
    }

    public override async Task<Results<Ok<TagDto>, NotFound>> ExecuteAsync(UpdateTagRequest req, CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, tagService, ct);
    }

    internal static async Task<Results<Ok<TagDto>, NotFound>> HandleRequestAsync(
        long id,
        UpdateTagRequest request,
        ITagService tagService,
        CancellationToken ct)
    {
        var tag = await tagService.UpdateTagAsync(id, request, ct);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(tag);
    }
}
