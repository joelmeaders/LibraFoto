using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Admin;

/// <summary>
/// Paginated list of guest links (admin/editor).
/// </summary>
public sealed class GetGuestLinksEndpoint : Endpoint<GetGuestLinksRequest, Ok<PagedResult<GuestLinkDto>>>
{
    public override void Configure()
    {
        Get("/api/admin/guest-links");
        Roles(UserRole.Admin.ToString(), UserRole.Editor.ToString());
        Tags("Guest Link Management");
        Summary(s =>
        {
            s.Summary = "Get all guest links";
            s.Description = "Returns a paginated list of all guest links.";
        });
    }

    public override async Task<Ok<PagedResult<GuestLinkDto>>> ExecuteAsync(GetGuestLinksRequest req, CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, guestLinkService, ct);
    }

    internal static async Task<Ok<PagedResult<GuestLinkDto>>> HandleRequestAsync(
        GetGuestLinksRequest request,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 20;
        }

        var (links, totalCount) = await guestLinkService.GetGuestLinksAsync(
            page, pageSize, request.IncludeExpired, cancellationToken);
        var linkArray = links.ToArray();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pagination = new PaginationInfo(page, pageSize, totalCount, totalPages);

        return TypedResults.Ok(new PagedResult<GuestLinkDto>(linkArray, pagination));
    }
}

public sealed class GetGuestLinksRequest
{
    [QueryParam]
    public int Page { get; init; }

    [QueryParam]
    public int PageSize { get; init; }

    [QueryParam]
    public bool IncludeExpired { get; init; }
}
