using LibraFoto.Modules.Auth.Services.Shared;

namespace LibraFoto.Modules.Auth.Services.Repositories;

/// <summary>
/// Repository abstraction for guest link data access operations.
/// </summary>
public interface IGuestLinkRepository
{
    Task<GuestLinkDto> CreateGuestLinkAsync(
        CreateGuestLinkRequest request,
        long createdByUserId,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<GuestLinkDto> Links, int TotalCount)> GetGuestLinksAsync(
        int page = 1,
        int pageSize = 20,
        bool includeExpired = false,
        CancellationToken cancellationToken = default);

    Task<GuestLinkDto?> GetGuestLinkByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<GuestLinkValidationResponse> ValidateGuestLinkAsync(string linkCode, CancellationToken cancellationToken = default);

    Task<bool> RecordUploadAsync(string linkCode, CancellationToken cancellationToken = default);

    Task<bool> DeleteGuestLinkAsync(string id, CancellationToken cancellationToken = default);

    Task<IEnumerable<GuestLinkDto>> GetGuestLinksByUserAsync(long userId, CancellationToken cancellationToken = default);
}
