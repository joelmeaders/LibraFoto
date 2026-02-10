using FastEndpoints;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.GuestLinks.Public;

/// <summary>
/// Validate a guest link code.
/// </summary>
public sealed class ValidateGuestLinkEndpoint : Endpoint<ValidateGuestLinkRequest, Ok<GuestLinkValidationResponse>>
{
    public override void Configure()
    {
        Get("/api/guest/{linkCode}/validate");
        AllowAnonymous();
        Tags("Guest Access");
        Summary(s =>
        {
            s.Summary = "Validate a guest link";
            s.Description = "Validates a guest link code and returns its status.";
        });
    }

    public override async Task<Ok<GuestLinkValidationResponse>> ExecuteAsync(ValidateGuestLinkRequest req, CancellationToken ct)
    {
        var guestLinkService = Resolve<IGuestLinkService>();
        return await HandleRequestAsync(req, guestLinkService, ct);
    }

    internal static async Task<Ok<GuestLinkValidationResponse>> HandleRequestAsync(
        ValidateGuestLinkRequest request,
        IGuestLinkService guestLinkService,
        CancellationToken cancellationToken)
    {
        var result = await guestLinkService.ValidateGuestLinkAsync(request.LinkCode, cancellationToken);
        return TypedResults.Ok(result);
    }
}

public sealed class ValidateGuestLinkRequest
{
    [BindFrom("linkCode")]
    public string LinkCode { get; init; } = string.Empty;
}
