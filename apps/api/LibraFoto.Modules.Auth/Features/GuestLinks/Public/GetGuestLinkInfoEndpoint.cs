using FastEndpoints;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Public;

/// <summary>
/// Get public info about a guest link.
/// </summary>
public sealed class GetGuestLinkInfoEndpoint : Endpoint<GetGuestLinkInfoRequest, Results<Ok<GuestLinkPublicInfo>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/guest/{linkCode}");
        AllowAnonymous();
        Tags("Guest Access");
        Summary(s =>
        {
            s.Summary = "Get guest link info";
            s.Description = "Gets public information about a guest link.";
        });
    }

    public override async Task<Results<Ok<GuestLinkPublicInfo>, NotFound>> ExecuteAsync(GetGuestLinkInfoRequest req, CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, guestLinkService, ct);
    }

    internal static async Task<Results<Ok<GuestLinkPublicInfo>, NotFound>> HandleRequestAsync(
        GetGuestLinkInfoRequest request,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var validation = await guestLinkService.ValidateGuestLinkAsync(request.LinkCode, cancellationToken);

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

public sealed class GetGuestLinkInfoRequest
{
    [BindFrom("linkCode")]
    public string LinkCode { get; init; } = string.Empty;
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
