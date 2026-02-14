using FastEndpoints;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Modules.Admin.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Admin.Features.Tags;

/// <summary>
/// Create a new tag.
/// </summary>
public sealed class CreateTagEndpoint : Endpoint<CreateTagRequest, Created<TagDto>>
{
    public override void Configure()
    {
        Post("/api/admin/tags");
        Tags("Tags");
        Summary(s =>
        {
            s.Summary = "Create a new tag";
        });
    }

    public override async Task<Created<TagDto>> ExecuteAsync(CreateTagRequest req, CancellationToken ct)
    {
        var tagService = Resolve<ITagService>();
        return await HandleRequestAsync(req, tagService, ct);
    }

    internal static async Task<Created<TagDto>> HandleRequestAsync(
        CreateTagRequest request,
        ITagService tagService,
        CancellationToken ct)
    {
        var tag = await tagService.CreateTagAsync(request, ct);
        return TypedResults.Created($"/api/admin/tags/{tag.Id}", tag);
    }
}
