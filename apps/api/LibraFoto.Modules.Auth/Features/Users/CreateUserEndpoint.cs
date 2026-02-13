using FastEndpoints;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Modules.Auth.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraFoto.Modules.Auth.Features.Users;

/// <summary>
/// Create a new user (admin only).
/// </summary>
public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>>
{
    public override void Configure()
    {
        Post("/api/admin/users");
        Roles(UserRole.Admin.ToString());
        Tags("User Management");
        Summary(s =>
        {
            s.Summary = "Create a new user";
            s.Description = "Creates a new user with the specified role. Admin only.";
        });
    }

    public override async Task<Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>> ExecuteAsync(
        CreateUserRequest req,
        CancellationToken ct)
    {
        var userService = Resolve<IUserService>();
        return await HandleRequestAsync(req, userService, ct);
    }

    internal static async Task<Results<Created<UserDto>, Conflict<ApiError>, ValidationProblem>> HandleRequestAsync(
        CreateUserRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateUserRequest(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var user = await userService.CreateUserAsync(request, cancellationToken);
            return TypedResults.Created($"/api/admin/users/{user.Id}", user);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new ApiError("CREATE_FAILED", ex.Message));
        }
    }

    private static Dictionary<string, string[]> ValidateCreateUserRequest(CreateUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = new[] { "Email is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = new[] { "Password is required." };
        }
        else if (request.Password.Length < 6)
        {
            errors["password"] = new[] { "Password must be at least 6 characters." };
        }

        return errors;
    }
}
