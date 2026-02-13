using FastEndpoints;
using LibraFoto.Modules.Admin.Features.Shared;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Get a tag by ID.
/// </summary>
public sealed class GetTagByIdEndpoint : Endpoint<GetTagByIdRequest, Results<Ok<TagDto>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/admin/tags/{id:long}");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Get a tag by ID";
        });
    }

    public override async Task<Results<Ok<TagDto>, NotFound>> ExecuteAsync(GetTagByIdRequest req, CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        return await HandleRequestAsync(req.Id, tagService, ct);
    }

    internal static async Task<Results<Ok<TagDto>, NotFound>> HandleRequestAsync(
        long id,
        ITagService tagService,
        CancellationToken ct)
    {
        var tag = await tagService.GetTagByIdAsync(id, ct);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(tag);
    }
}

public sealed class GetTagByIdRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
