using System.Security.Claims;
using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Admin;

/// <summary>
/// Get guest links created by the current user (admin/editor).
/// </summary>
public sealed class GetMyGuestLinksEndpoint : EndpointWithoutRequest<Results<Ok<GuestLinkDto[]>, UnauthorizedHttpResult>>
{
    public override void Configure()
    {
        Get("/api/admin/guest-links/my-links");
        Roles(UserRole.Admin.ToString(), UserRole.Editor.ToString());
        Tags("Guest Link Management");
        Summary(s =>
        {
            s.Summary = "Get my guest links";
            s.Description = "Returns guest links created by the current user.";
        });
    }

    public override async Task<Results<Ok<GuestLinkDto[]>, UnauthorizedHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(User, guestLinkService, ct);
    }

    internal static async Task<Results<Ok<GuestLinkDto[]>, UnauthorizedHttpResult>> HandleRequestAsync(
        ClaimsPrincipal user,
        IGuestLinkService guestLinkService,
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
}
