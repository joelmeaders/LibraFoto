using LibraFoto.Modules.Admin.Models;
using LibraFoto.Modules.Admin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Admin.Endpoints
{
    /// <summary>
    /// Endpoints for album management operations.
    /// </summary>
    public static class AlbumEndpoints
    {
        /// <summary>
        /// Maps album management endpoints to the route builder.
        /// </summary>
        public static IEndpointRouteBuilder MapAlbumEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/albums")
                .WithTags("Albums");

            group.MapGet("/", GetAlbums)
                .WithName("GetAlbums")
                .WithSummary("Get all albums");

            group.MapGet("/{id:long}", GetAlbumById)
                .WithName("GetAlbumById")
                .WithSummary("Get an album by ID");

            group.MapPost("/", CreateAlbum)
                .WithName("CreateAlbum")
                .WithSummary("Create a new album");

            group.MapPut("/{id:long}", UpdateAlbum)
                .WithName("UpdateAlbum")
                .WithSummary("Update an album");

            group.MapDelete("/{id:long}", DeleteAlbum)
                .WithName("DeleteAlbum")
                .WithSummary("Delete an album");

            group.MapPut("/{id:long}/cover/{photoId:long}", SetCoverPhoto)
                .WithName("SetAlbumCoverPhoto")
                .WithSummary("Set the cover photo for an album");

            group.MapDelete("/{id:long}/cover", RemoveCoverPhoto)
                .WithName("RemoveAlbumCoverPhoto")
                .WithSummary("Remove the cover photo from an album");

            group.MapPost("/{id:long}/photos", AddPhotosToAlbum)
                .WithName("AddPhotosToAlbum")
                .WithSummary("Add photos to an album");

            group.MapDelete("/{id:long}/photos", RemovePhotosFromAlbum)
                .WithName("RemovePhotosFromAlbum")
                .WithSummary("Remove photos from an album");

            group.MapPut("/{id:long}/photos/reorder", ReorderPhotos)
                .WithName("ReorderPhotosInAlbum")
                .WithSummary("Reorder photos in an album");

            return app;
        }

        private static async Task<Ok<IReadOnlyList<AlbumDto>>> GetAlbums(
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var albums = await albumService.GetAlbumsAsync(ct);
            return TypedResults.Ok(albums);
        }

        private static async Task<Results<Ok<AlbumDto>, NotFound>> GetAlbumById(
            long id,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var album = await albumService.GetAlbumByIdAsync(id, ct);
            if (album is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(album);
        }

        private static async Task<Created<AlbumDto>> CreateAlbum(
            CreateAlbumRequest request,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var album = await albumService.CreateAlbumAsync(request, ct);
            return TypedResults.Created($"/api/admin/albums/{album.Id}", album);
        }

        private static async Task<Results<Ok<AlbumDto>, NotFound>> UpdateAlbum(
            long id,
            [FromBody] UpdateAlbumRequest request,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var album = await albumService.UpdateAlbumAsync(id, request, ct);
            if (album is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(album);
        }

        private static async Task<Results<NoContent, NotFound>> DeleteAlbum(
            long id,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var deleted = await albumService.DeleteAlbumAsync(id, ct);
            if (!deleted)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }

        private static async Task<Results<Ok<AlbumDto>, NotFound>> SetCoverPhoto(
            long id,
            long photoId,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var album = await albumService.SetCoverPhotoAsync(id, photoId, ct);
            if (album is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(album);
        }

        private static async Task<Results<Ok<AlbumDto>, NotFound>> RemoveCoverPhoto(
            long id,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var album = await albumService.RemoveCoverPhotoAsync(id, ct);
            if (album is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(album);
        }

        private static async Task<Ok<BulkOperationResult>> AddPhotosToAlbum(
            long id,
            [FromBody] AddPhotosToAlbumRequest request,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var result = await albumService.AddPhotosAsync(id, request.PhotoIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Ok<BulkOperationResult>> RemovePhotosFromAlbum(
            long id,
            [FromBody] RemovePhotosFromAlbumRequest request,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var result = await albumService.RemovePhotosAsync(id, request.PhotoIds, ct);
            return TypedResults.Ok(result);
        }

        private static async Task<Results<NoContent, NotFound>> ReorderPhotos(
            long id,
            [FromBody] ReorderPhotosRequest request,
            IAlbumService albumService,
            CancellationToken ct = default)
        {
            var success = await albumService.ReorderPhotosAsync(id, request.PhotoOrders, ct);
            if (!success)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }
    }
}
