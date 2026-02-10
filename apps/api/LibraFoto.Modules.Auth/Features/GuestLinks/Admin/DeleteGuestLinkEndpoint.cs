using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Admin;

/// <summary>
/// Delete a guest link (admin/editor).
/// </summary>
public sealed class DeleteGuestLinkEndpoint : Endpoint<DeleteGuestLinkRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("/api/admin/guest-links/{id}");
        Roles(UserRole.Admin.ToString(), UserRole.Editor.ToString());
        Tags("Guest Link Management");
        Summary(s =>
        {
            s.Summary = "Delete a guest link";
            s.Description = "Deletes a guest link.";
        });
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(DeleteGuestLinkRequest req, CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, guestLinkService, ct);
    }

    internal static async Task<Results<NoContent, NotFound>> HandleRequestAsync(
        DeleteGuestLinkRequest request,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var result = await guestLinkService.DeleteGuestLinkAsync(request.Id, cancellationToken);

        if (!result)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}

public sealed class DeleteGuestLinkRequest
{
    [BindFrom("id")]
    public string Id { get; init; } = string.Empty;
}
