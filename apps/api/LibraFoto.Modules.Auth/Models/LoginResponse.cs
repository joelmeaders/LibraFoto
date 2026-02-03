namespace LibraFoto.Modules.Auth.Models;

/// <summary>
/// Response model for successful login.
/// </summary>
public record LoginResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);
