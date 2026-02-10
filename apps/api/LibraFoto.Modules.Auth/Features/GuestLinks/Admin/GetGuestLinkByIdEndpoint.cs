using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Admin;

/// <summary>
/// Get a guest link by ID (admin/editor).
/// </summary>
public sealed class GetGuestLinkByIdEndpoint : Endpoint<GetGuestLinkByIdRequest, Results<Ok<GuestLinkDto>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/admin/guest-links/{id}");
        Roles(UserRole.Admin.ToString(), UserRole.Editor.ToString());
        Tags("Guest Link Management");
        Summary(s =>
        {
            s.Summary = "Get guest link by ID";
            s.Description = "Returns a specific guest link by its ID.";
        });
    }

    public override async Task<Results<Ok<GuestLinkDto>, NotFound>> ExecuteAsync(GetGuestLinkByIdRequest req, CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, guestLinkService, ct);
    }

    internal static async Task<Results<Ok<GuestLinkDto>, NotFound>> HandleRequestAsync(
        GetGuestLinkByIdRequest request,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var link = await guestLinkService.GetGuestLinkByIdAsync(request.Id, cancellationToken);

        if (link == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(link);
    }
}

public sealed class GetGuestLinkByIdRequest
{
    [BindFrom("id")]
    public string Id { get; init; } = string.Empty;
}
