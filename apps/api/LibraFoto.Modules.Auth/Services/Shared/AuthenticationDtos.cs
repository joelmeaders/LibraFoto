using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Services.Shared;

/// <summary>
/// Request model for user login.
/// </summary>
public record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(255)]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 6)]
    string Password
);

/// <summary>
/// Response model for successful login.
/// </summary>
public record LoginResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);
