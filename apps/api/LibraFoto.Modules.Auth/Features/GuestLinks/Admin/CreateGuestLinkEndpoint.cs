using System.Security.Claims;
using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Admin;

/// <summary>
/// Create a guest upload link (admin/editor).
/// </summary>
public sealed class CreateGuestLinkEndpoint : Endpoint<CreateGuestLinkRequest, Results<Created<GuestLinkDto>, UnauthorizedHttpResult, ValidationProblem>>
{
    public override void Configure()
    {
        Post("/api/admin/guest-links");
        Roles(UserRole.Admin.ToString(), UserRole.Editor.ToString());
        Tags("Guest Link Management");
        Summary(s =>
        {
            s.Summary = "Create a guest link";
            s.Description = "Creates a new guest upload link.";
        });
    }

    public override async Task<Results<Created<GuestLinkDto>, UnauthorizedHttpResult, ValidationProblem>> ExecuteAsync(
        CreateGuestLinkRequest req,
        CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, User, guestLinkService, ct);
    }

    internal static async Task<Results<Created<GuestLinkDto>, UnauthorizedHttpResult, ValidationProblem>> HandleRequestAsync(
        CreateGuestLinkRequest request,
        ClaimsPrincipal user,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return TypedResults.Unauthorized();
        }

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
}
