using System.Security.Claims;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LibraFoto.Modules.Auth.Endpoints
{
    /// <summary>
    /// Guest link management endpoints.
    /// </summary>
    public static class GuestLinkEndpoints
    {
        /// <summary>
        /// Maps guest link management endpoints to the route builder.
        /// </summary>
        public static IEndpointRouteBuilder MapGuestLinkEndpoints(this IEndpointRouteBuilder app)
        {
            // Admin endpoints for managing guest links
            var adminGroup = app.MapGroup("/api/admin/guest-links")
                .WithTags("Guest Link Management")
                .RequireAuthorization(policy => policy.RequireRole(
                    UserRole.Admin.ToString(),
                    UserRole.Editor.ToString()));

            adminGroup.MapGet("/", GetGuestLinks)
                .WithName("GetGuestLinks")
                .WithSummary("Get all guest links")
                .WithDescription("Returns a paginated list of all guest links.");

            adminGroup.MapGet("/{id}", GetGuestLinkById)
                .WithName("GetGuestLinkById")
                .WithSummary("Get guest link by ID")
                .WithDescription("Returns a specific guest link by its ID.");

            adminGroup.MapPost("/", CreateGuestLink)
                .WithName("CreateGuestLink")
                .WithSummary("Create a guest link")
                .WithDescription("Creates a new guest upload link.");

            adminGroup.MapDelete("/{id}", DeleteGuestLink)
                .WithName("DeleteGuestLink")
                .WithSummary("Delete a guest link")
                .WithDescription("Deletes a guest link.");

            adminGroup.MapGet("/my-links", GetMyGuestLinks)
                .WithName("GetMyGuestLinks")
                .WithSummary("Get my guest links")
                .WithDescription("Returns guest links created by the current user.");

            // Public endpoints for validating and using guest links
            var publicGroup = app.MapGroup("/api/guest")
                .WithTags("Guest Access")
                .AllowAnonymous();

            publicGroup.MapGet("/{linkCode}/validate", ValidateGuestLink)
                .WithName("ValidateGuestLink")
                .WithSummary("Validate a guest link")
                .WithDescription("Validates a guest link code and returns its status.");

            publicGroup.MapGet("/{linkCode}", GetGuestLinkInfo)
                .WithName("GetGuestLinkInfo")
                .WithSummary("Get guest link info")
                .WithDescription("Gets public information about a guest link.");

            return app;
        }

        /// <summary>
        /// Gets all guest links with pagination.
        /// </summary>
        private static async Task<Ok<PagedResult<GuestLinkDto>>> GetGuestLinks(
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] bool includeExpired = false,
            [FromServices] IGuestLinkService guestLinkService = null!,
            CancellationToken cancellationToken = default)
        {
            // Default values
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1 || pageSize > 100)
            {
                pageSize = 20;
            }

            var (links, totalCount) = await guestLinkService.GetGuestLinksAsync(
                page, pageSize, includeExpired, cancellationToken);
            var linkArray = links.ToArray();

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var pagination = new PaginationInfo(page, pageSize, totalCount, totalPages);

            return TypedResults.Ok(new PagedResult<GuestLinkDto>(linkArray, pagination));
        }

        /// <summary>
        /// Gets a guest link by ID.
        /// </summary>
        private static async Task<Results<Ok<GuestLinkDto>, NotFound>> GetGuestLinkById(
            string id,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            var link = await guestLinkService.GetGuestLinkByIdAsync(id, cancellationToken);

            if (link == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(link);
        }

        /// <summary>
        /// Creates a new guest link.
        /// </summary>
        private static async Task<Results<Created<GuestLinkDto>, UnauthorizedHttpResult, ValidationProblem>> CreateGuestLink(
            [FromBody] CreateGuestLinkRequest request,
            ClaimsPrincipal user,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            // Get current user ID
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            // Validate request
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors["name"] = new[] { "Name is required." };
            }

            if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.UtcNow)
            {
                errors["expiresAt"] = new[] { "Expiration date must be in the future." };
            }

            if (request.MaxUploads.HasValue && request.MaxUploads.Value < 1)
            {
                errors["maxUploads"] = new[] { "Maximum uploads must be at least 1." };
            }

            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var link = await guestLinkService.CreateGuestLinkAsync(request, userId, cancellationToken);
            return TypedResults.Created($"/api/admin/guest-links/{link.Id}", link);
        }

        /// <summary>
        /// Deletes a guest link.
        /// </summary>
        private static async Task<Results<NoContent, NotFound>> DeleteGuestLink(
            string id,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            var result = await guestLinkService.DeleteGuestLinkAsync(id, cancellationToken);

            if (!result)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.NoContent();
        }

        /// <summary>
        /// Gets guest links created by the current user.
        /// </summary>
        private static async Task<Results<Ok<GuestLinkDto[]>, UnauthorizedHttpResult>> GetMyGuestLinks(
            ClaimsPrincipal user,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return TypedResults.Unauthorized();
            }

            var links = await guestLinkService.GetGuestLinksByUserAsync(userId, cancellationToken);
            return TypedResults.Ok(links.ToArray());
        }

        /// <summary>
        /// Validates a guest link code.
        /// </summary>
        private static async Task<Ok<GuestLinkValidationResponse>> ValidateGuestLink(
            string linkCode,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            var result = await guestLinkService.ValidateGuestLinkAsync(linkCode, cancellationToken);
            return TypedResults.Ok(result);
        }

        /// <summary>
        /// Gets public information about a guest link.
        /// </summary>
        private static async Task<Results<Ok<GuestLinkPublicInfo>, NotFound>> GetGuestLinkInfo(
            string linkCode,
            [FromServices] IGuestLinkService guestLinkService,
            CancellationToken cancellationToken)
        {
            var validation = await guestLinkService.ValidateGuestLinkAsync(linkCode, cancellationToken);

            if (!validation.IsValid && validation.Name == null)
            {
                return TypedResults.NotFound();
            }

            var info = new GuestLinkPublicInfo(
                validation.Name!,
                validation.TargetAlbumName,
                validation.IsValid,
                validation.RemainingUploads,
                validation.Message);

            return TypedResults.Ok(info);
        }
    }

    /// <summary>
    /// Public information about a guest link (without sensitive data).
    /// </summary>
    public record GuestLinkPublicInfo(
        string Name,
        string? TargetAlbumName,
        bool IsActive,
        int? RemainingUploads,
        string? StatusMessage);
}
