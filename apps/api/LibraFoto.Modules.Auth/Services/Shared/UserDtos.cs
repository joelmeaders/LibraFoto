using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Auth.Services.Shared;

/// <summary>
/// User data transfer object (without sensitive data).
/// </summary>
public record UserDto(
    long Id,
    string Email,
    UserRole Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);
