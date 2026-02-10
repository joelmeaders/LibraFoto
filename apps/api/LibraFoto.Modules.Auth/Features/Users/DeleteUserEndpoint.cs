using System.Security.Claims;
using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Users;

/// <summary>
/// Delete a user (admin only).
/// </summary>
public sealed class DeleteUserEndpoint : EndpointWithoutRequest<Results<NoContent, NotFound, Conflict<ApiError>>>
{
    public override void Configure()
    {
        Delete("/api/admin/users/{id:long}");
        Roles(UserRole.Admin.ToString());
        Tags("User Management");
        Summary(s =>
        {
            s.Summary = "Delete a user";
            s.Description = "Deletes a user. Cannot delete yourself. Admin only.";
        });
    }

    public override async Task<Results<NoContent, NotFound, Conflict<ApiError>>> ExecuteAsync(CancellationToken ct)
    {
        var userService = Resolve<IUserService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, User, userService, ct);
    }

    internal static async Task<Results<NoContent, NotFound, Conflict<ApiError>>> HandleRequestAsync(
        long id,
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var currentUserIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim != null &&
            long.TryParse(currentUserIdClaim.Value, out var currentUserId) &&
            currentUserId == id)
        {
            return TypedResults.Conflict(new ApiError(
                "CANNOT_DELETE_SELF",
                "You cannot delete your own account."));
        }

        var result = await userService.DeleteUserAsync(id, cancellationToken);

        if (!result)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
