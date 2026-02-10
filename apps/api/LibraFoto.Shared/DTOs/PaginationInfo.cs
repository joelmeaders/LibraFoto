namespace LibraFoto.Shared.DTOs;

/// <summary>
/// Pagination metadata for paginated API responses.
/// </summary>
public record PaginationInfo(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
