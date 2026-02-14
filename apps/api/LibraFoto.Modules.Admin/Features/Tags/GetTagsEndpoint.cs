using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// List all tags.
/// </summary>
public sealed class GetTagsEndpoint : EndpointWithoutRequest<Ok<IReadOnlyList<TagDto>>>
{
    public override void Configure()
    {
        Get("/api/admin/tags");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Get all tags";
        });
    }

    public override async Task<Ok<IReadOnlyList<TagDto>>> ExecuteAsync(CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        return await HandleRequestAsync(tagService, ct);
    }

    internal static async Task<Ok<IReadOnlyList<TagDto>>> HandleRequestAsync(ITagService tagService, CancellationToken ct)
    {
        var tags = await tagService.GetTagsAsync(ct);
        return TypedResults.Ok(tags);
    }
}
