using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Delete a tag.
/// </summary>
public sealed class DeleteTagEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("/api/admin/tags/{id:long}");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Delete a tag";
        });
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, tagService, ct);
    }

    internal static async Task<Results<NoContent, NotFound>> HandleRequestAsync(
        long id,
        ITagService tagService,
        CancellationToken ct)
    {
        var deleted = await tagService.DeleteTagAsync(id, ct);
        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
