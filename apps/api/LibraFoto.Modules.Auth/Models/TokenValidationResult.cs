namespace LibraFoto.Modules.Auth.Models;

/// <summary>
/// Result of token validation.
/// </summary>
public record TokenValidationResult(bool IsValid, long? UserId);
