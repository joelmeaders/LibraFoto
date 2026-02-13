using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Modules.Auth.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Users;

/// <summary>
/// Update a user (admin only).
/// </summary>
public sealed class UpdateUserEndpoint : Endpoint<UpdateUserRequest, Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>>
{
    public override void Configure()
    {
        Put("/api/admin/users/{id:long}");
        Roles(UserRole.Admin.ToString());
        Tags("User Management");
        Summary(s =>
        {
            s.Summary = "Update a user";
            s.Description = "Updates an existing user's information. Admin only.";
        });
    }

    public override async Task<Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>> ExecuteAsync(
        UpdateUserRequest req,
        CancellationToken ct)
    {
        var userService = Resolve<IUserService>();
        var id = Route<long>("id");
        return await HandleRequestAsync(id, req, userService, ct);
    }

    internal static async Task<Results<Ok<UserDto>, NotFound, Conflict<ApiError>, ValidationProblem>> HandleRequestAsync(
        long id,
        UpdateUserRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateUserRequest(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var user = await userService.UpdateUserAsync(id, request, cancellationToken);

            if (user == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new ApiError("UPDATE_FAILED", ex.Message));
        }
    }

    private static Dictionary<string, string[]> ValidateUpdateUserRequest(UpdateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Password != null && request.Password.Length < 6)
        {
            errors["password"] = new[] { "Password must be at least 6 characters." };
        }

        return errors;
    }
}
