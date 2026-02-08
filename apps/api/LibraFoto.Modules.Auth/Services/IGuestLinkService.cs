using LibraFoto.Modules.Auth.Models;

namespace LibraFoto.Modules.Auth.Services
{
    /// <summary>
    /// Service interface for guest link management.
    /// </summary>
    public interface IGuestLinkService
    {
        /// <summary>
        /// Creates a new guest upload link.
        /// </summary>
        /// <param name="request">Guest link creation request.</param>
        /// <param name="createdByUserId">The ID of the user creating the link.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Created guest link DTO.</returns>
        Task<GuestLinkDto> CreateGuestLinkAsync(
            CreateGuestLinkRequest request,
            long createdByUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all guest links with optional pagination.
        /// </summary>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <param name="includeExpired">Whether to include expired links.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paged list of guest links.</returns>
        Task<(IEnumerable<GuestLinkDto> Links, int TotalCount)> GetGuestLinksAsync(
            int page = 1,
            int pageSize = 20,
            bool includeExpired = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a guest link by its ID.
        /// </summary>
        /// <param name="id">The guest link ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Guest link DTO or null if not found.</returns>
        Task<GuestLinkDto?> GetGuestLinkByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a guest link by its link code.
        /// </summary>
        /// <param name="linkCode">The unique link code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Guest link DTO or null if not found.</returns>
        Task<GuestLinkDto?> GetGuestLinkByCodeAsync(string linkCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a guest link code for use.
        /// </summary>
        /// <param name="linkCode">The link code to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation response with status and details.</returns>
        Task<GuestLinkValidationResponse> ValidateGuestLinkAsync(string linkCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records an upload against a guest link.
        /// </summary>
        /// <param name="linkCode">The link code used for upload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if recorded successfully, false if link is invalid or exhausted.</returns>
        Task<bool> RecordUploadAsync(string linkCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a guest link.
        /// </summary>
        /// <param name="id">The guest link ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted, false if not found.</returns>
        Task<bool> DeleteGuestLinkAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets guest links created by a specific user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of guest links created by the user.</returns>
        Task<IEnumerable<GuestLinkDto>> GetGuestLinksByUserAsync(long userId, CancellationToken cancellationToken = default);
    }
}
