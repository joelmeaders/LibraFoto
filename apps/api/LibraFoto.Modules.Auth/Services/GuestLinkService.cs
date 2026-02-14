using LibraFoto.Modules.Auth.Services.Repositories;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// Guest link management service implementation using EF Core and SQLite.
/// </summary>
public class GuestLinkService : IGuestLinkService
{
    private readonly IGuestLinkRepository _guestLinkRepository;
    private readonly ILogger<GuestLinkService> _logger;

    public GuestLinkService(
        IGuestLinkRepository guestLinkRepository,
        ILogger<GuestLinkService> logger)
    {
        _guestLinkRepository = guestLinkRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GuestLinkDto> CreateGuestLinkAsync(
        CreateGuestLinkRequest request,
        long createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await _guestLinkRepository.CreateGuestLinkAsync(request, createdByUserId, cancellationToken);
        _logger.LogInformation("Created guest link: {LinkId} by user {UserId}", result.Id, createdByUserId);
        return result;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GuestLinkDto> Links, int TotalCount)> GetGuestLinksAsync(
        int page = 1,
        int pageSize = 20,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        return await _guestLinkRepository.GetGuestLinksAsync(page, pageSize, includeExpired, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GuestLinkDto?> GetGuestLinkByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _guestLinkRepository.GetGuestLinkByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GuestLinkDto?> GetGuestLinkByCodeAsync(string linkCode, CancellationToken cancellationToken = default)
    {
        // Link code is the same as the ID (NanoId)
        return await GetGuestLinkByIdAsync(linkCode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GuestLinkValidationResponse> ValidateGuestLinkAsync(string linkCode, CancellationToken cancellationToken = default)
    {
        return await _guestLinkRepository.ValidateGuestLinkAsync(linkCode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RecordUploadAsync(string linkCode, CancellationToken cancellationToken = default)
    {
        var success = await _guestLinkRepository.RecordUploadAsync(linkCode, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Recorded upload for guest link: {LinkId}", linkCode);
        }

        return success;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteGuestLinkAsync(string id, CancellationToken cancellationToken = default)
    {
        var deleted = await _guestLinkRepository.DeleteGuestLinkAsync(id, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation("Deleted guest link: {LinkId}", id);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GuestLinkDto>> GetGuestLinksByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _guestLinkRepository.GetGuestLinksByUserAsync(userId, cancellationToken);
    }
}
