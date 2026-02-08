using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Filter options for photo list queries.
    /// </summary>
    public record PhotoFilterRequest
    {
        /// <summary>
        /// Page number (1-based).
        /// </summary>
        public int Page { get; init; } = 1;

        /// <summary>
        /// Number of items per page.
        /// </summary>
        public int PageSize { get; init; } = 50;

        /// <summary>
        /// Filter by album ID.
        /// </summary>
        public long? AlbumId { get; init; }

        /// <summary>
        /// Filter by tag ID.
        /// </summary>
        public long? TagId { get; init; }

        /// <summary>
        /// Filter by start date (DateTaken).
        /// </summary>
        public DateTime? DateFrom { get; init; }

        /// <summary>
        /// Filter by end date (DateTaken).
        /// </summary>
        public DateTime? DateTo { get; init; }

        /// <summary>
        /// Filter by media type.
        /// </summary>
        public MediaType? MediaType { get; init; }

        /// <summary>
        /// Search by filename.
        /// </summary>
        public string? Search { get; init; }

        /// <summary>
        /// Sort field (DateTaken, DateAdded, Filename).
        /// </summary>
        public string SortBy { get; init; } = "DateAdded";

        /// <summary>
        /// Sort direction (asc, desc).
        /// </summary>
        public string SortDirection { get; init; } = "desc";
    }
}
