namespace LibraFoto.Shared.DTOs
{
    /// <summary>
    /// Generic wrapper for paginated API responses.
    /// </summary>
    /// <typeparam name="T">The type of items in the result.</typeparam>
    public record PagedResult<T>(
        T[] Data,
        PaginationInfo Pagination
    );
}
