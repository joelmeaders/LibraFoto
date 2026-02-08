using LibraFoto.Data.Enums;
using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Admin.Endpoints
{
    /// <summary>
    /// Endpoints for photo management operations.
    /// </summary>
    public static class PhotoEndpoints
    {
        /// <summary>
        /// Maps photo management endpoints to the route builder.
        /// </summary>
        public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/photos")
                .WithTags("Photos");

            group.MapGet("/", GetPhotos)
                .WithName("GetPhotos")
                .WithSummary("Get paginated list of photos with optional filtering");

            group.MapGet("/count", GetPhotoCount)
                .WithName("GetAdminPhotoCount")
                .WithSummary("Get total count of photos");

            group.MapGet("/{id:long}", GetPhotoById)
                .WithName("GetPhotoById")
                .WithSummary("Get detailed information about a photo");

            group.MapPut("/{id:long}", UpdatePhoto)
                .WithName("UpdatePhoto")
                .WithSummary("Update a photo's metadata");

            group.MapDelete("/{id:long}", DeletePhoto)
                .WithName("DeletePhoto")
                .WithSummary("Delete a single photo");

            group.MapPost("/bulk/delete", BulkDeletePhotos)
                .WithName("BulkDeletePhotos")
                .WithSummary("Delete multiple photos");

            group.MapPost("/bulk/add-to-album/{albumId:long}", BulkAddToAlbum)
                .WithName("BulkAddToAlbum")
                .WithSummary("Add multiple photos to an album");

            group.MapPost("/bulk/remove-from-album/{albumId:long}", BulkRemoveFromAlbum)
                .WithName("BulkRemoveFromAlbum")
                .WithSummary("Remove multiple photos from an album");

            group.MapPost("/bulk/add-tags", BulkAddTags)
                .WithName("BulkAddTags")
                .WithSummary("Add tags to multiple photos");

            group.MapPost("/bulk/remove-tags", BulkRemoveTags)
                .WithName("BulkRemoveTags")
                .WithSummary("Remove tags from multiple photos");

            return app;
        }

        private static async Task<Ok<PagedResult<PhotoListDto>>> GetPhotos(
            IPhotoService photoService,
            int page = 1,
            int pageSize = 50,
            long? albumId = null,
            long? tagId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            MediaType? mediaType = null,
            string? search = null,
            string sortBy = "DateAdded",
            string sortDirection = "desc",
            CancellationToken ct = default)
        {
            var filter = new PhotoFilterRequest
            {
                Page = page,
                PageSize = pageSize,
                AlbumId = albumId,
                TagId = tagId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                MediaType = mediaType,
                Search = search,
                SortBy = sortBy,
                SortDirection = sortDirection
            };

            var result = await photoService.GetPhotosAsync(filter, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<PhotoCountDto>> GetPhotoCount(
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.GetPhotoCountAsync(ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Results<Ok<PhotoDetailDto>, NotFound>> GetPhotoById(
            long id,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var photo = await photoService.GetPhotoByIdAsync(id, ct);
            if (photo is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(photo);
        }

        private static async Task<Results<Ok<PhotoDetailDto>, NotFound>> UpdatePhoto(
            long id,
            [FromBody] UpdatePhotoRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var photo = await photoService.UpdatePhotoAsync(id, request, ct);
            if (photo is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(photo);
        }

        private static async Task<Results<NoContent, NotFound>> DeletePhoto(
            long id,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var deleted = await photoService.DeletePhotoAsync(id, ct);
            if (!deleted)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }

        private static async Task<Ok<BulkOperationResult>> BulkDeletePhotos(
            BulkPhotoRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.DeletePhotosAsync(request.PhotoIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<BulkOperationResult>> BulkAddToAlbum(
            long albumId,
            [FromBody] AddPhotosToAlbumRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.AddPhotosToAlbumAsync(albumId, request.PhotoIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<BulkOperationResult>> BulkRemoveFromAlbum(
            long albumId,
            [FromBody] RemovePhotosFromAlbumRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.RemovePhotosFromAlbumAsync(albumId, request.PhotoIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<BulkOperationResult>> BulkAddTags(
            AddTagsToPhotosRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.AddTagsToPhotosAsync(request.PhotoIds, request.TagIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<BulkOperationResult>> BulkRemoveTags(
            RemoveTagsFromPhotosRequest request,
            IPhotoService photoService,
            CancellationToken ct = default)
        {
            var result = await photoService.RemoveTagsFromPhotosAsync(request.PhotoIds, request.TagIds, ct);
            return TypedResults.Ok(result);
        }
    }
}
