using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Models;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Users;

/// <summary>
/// Paginated list of all users (admin only).
/// </summary>
public sealed class GetUsersEndpoint : Endpoint<GetUsersRequest, Ok<PagedResult<UserDto>>>
{
    public override void Configure()
    {
        Get("/api/admin/users");
        Roles(UserRole.Admin.ToString());
        Tags("User Management");
        Summary(s =>
        {
            s.Summary = "Get all users";
            s.Description = "Returns a paginated list of all users. Admin only.";
        });
    }

    public override async Task<Ok<PagedResult<UserDto>>> ExecuteAsync(GetUsersRequest req, CancellationToken ct)
    {
        var userService = Resolve<IUserService>();
        return await HandleRequestAsync(req, userService, ct);
    }

    internal static async Task<Ok<PagedResult<UserDto>>> HandleRequestAsync(
        GetUsersRequest request,
        IUserService userService,
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

        var (users, totalCount) = await userService.GetUsersAsync(page, pageSize, cancellationToken);
        var userArray = users.ToArray();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pagination = new PaginationInfo(page, pageSize, totalCount, totalPages);

        return TypedResults.Ok(new PagedResult<UserDto>(userArray, pagination));
    }
}

public sealed class GetUsersRequest
{
    [QueryParam]
    public int Page { get; init; }

    [QueryParam]
    public int PageSize { get; init; }
}
