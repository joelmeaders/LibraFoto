using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Admin.Endpoints;

/// <summary>
/// Endpoints for tag management operations.
/// </summary>
public static class TagEndpoints
{
    /// <summary>
    /// Maps tag management endpoints to the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tags")
            .WithTags("Tags");

        group.MapGet("/", GetTags)
            .WithName("GetTags")
            .WithSummary("Get all tags");

        group.MapGet("/{id:long}", GetTagById)
            .WithName("GetTagById")
            .WithSummary("Get a tag by ID");

        group.MapPost("/", CreateTag)
            .WithName("CreateTag")
            .WithSummary("Create a new tag");

        group.MapPut("/{id:long}", UpdateTag)
            .WithName("UpdateTag")
            .WithSummary("Update a tag");

        group.MapDelete("/{id:long}", DeleteTag)
            .WithName("DeleteTag")
            .WithSummary("Delete a tag");

        group.MapPost("/{id:long}/photos", AddPhotosToTag)
            .WithName("AddPhotosToTag")
            .WithSummary("Add photos to a tag");

        group.MapDelete("/{id:long}/photos", RemovePhotosFromTag)
            .WithName("RemovePhotosFromTag")
            .WithSummary("Remove photos from a tag");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<TagDto>>> GetTags(
        ITagService tagService,
        CancellationToken ct = default)
    {
        var tags = await tagService.GetTagsAsync(ct);
        return TypedResults.Ok(tags);
    }

    private static async Task<Results<Ok<TagDto>, NotFound>> GetTagById(
        long id,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var tag = await tagService.GetTagByIdAsync(id, ct);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(tag);
    }

    private static async Task<Created<TagDto>> CreateTag(
        CreateTagRequest request,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var tag = await tagService.CreateTagAsync(request, ct);
        return TypedResults.Created($"/api/admin/tags/{tag.Id}", tag);
    }

    private static async Task<Results<Ok<TagDto>, NotFound>> UpdateTag(
        long id,
        [FromBody] UpdateTagRequest request,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var tag = await tagService.UpdateTagAsync(id, request, ct);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(tag);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTag(
        long id,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var deleted = await tagService.DeleteTagAsync(id, ct);
        if (!deleted)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BulkOperationResult>> AddPhotosToTag(
        long id,
        [FromBody] AddPhotosToTagRequest request,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var result = await tagService.AddPhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<BulkOperationResult>> RemovePhotosFromTag(
        long id,
        [FromBody] RemovePhotosFromTagRequest request,
        ITagService tagService,
        CancellationToken ct = default)
    {
        var result = await tagService.RemovePhotosAsync(id, request.PhotoIds, ct);
        return TypedResults.Ok(result);
    }
}
