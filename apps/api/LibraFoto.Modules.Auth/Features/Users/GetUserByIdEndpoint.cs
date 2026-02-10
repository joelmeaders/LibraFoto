using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Users;

/// <summary>
/// Get a user by ID (admin only).
/// </summary>
public sealed class GetUserByIdEndpoint : Endpoint<GetUserByIdRequest, Results<Ok<UserDto>, NotFound>>
{
    public override void Configure()
    {
        Get("/api/admin/users/{id:long}");
        Roles(UserRole.Admin.ToString());
        Tags("User Management");
        Summary(s =>
        {
            s.Summary = "Get user by ID";
            s.Description = "Returns a specific user by their ID. Admin only.";
        });
    }

    public override async Task<Results<Ok<UserDto>, NotFound>> ExecuteAsync(GetUserByIdRequest req, CancellationToken ct)
    {
        var userService = Resolve<IUserService>();
        return await HandleRequestAsync(req, userService, ct);
    }

    internal static async Task<Results<Ok<UserDto>, NotFound>> HandleRequestAsync(
        GetUserByIdRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(request.Id, cancellationToken);

        if (user == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(user);
    }
}

public sealed class GetUserByIdRequest
{
    [BindFrom("id")]
    public long Id { get; init; }
}
