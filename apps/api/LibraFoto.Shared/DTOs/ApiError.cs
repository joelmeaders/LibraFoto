namespace LibraFoto.Shared.DTOs;

/// <summary>
/// Standard error response format for API errors.
/// </summary>
public record ApiError(
    string Code,
    string Message,
    object? Details = null
);
