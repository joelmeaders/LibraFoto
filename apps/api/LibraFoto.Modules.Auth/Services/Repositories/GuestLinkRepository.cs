using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Auth.Services.Repositories;

/// <summary>
/// Entity Framework implementation of guest link repository.
/// </summary>
public sealed class GuestLinkRepository : IGuestLinkRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public GuestLinkRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GuestLinkDto> CreateGuestLinkAsync(
        CreateGuestLinkRequest request,
        long createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = new GuestLink
        {
            Name = request.Name,
            CreatedById = createdByUserId,
            DateCreated = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            MaxUploads = request.MaxUploads,
            CurrentUploads = 0,
            TargetAlbumId = request.TargetAlbumId
        };

        _dbContext.GuestLinks.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Entry(entity).Reference(e => e.CreatedBy).LoadAsync(cancellationToken);
        if (entity.TargetAlbumId.HasValue)
        {
            await _dbContext.Entry(entity).Reference(e => e.TargetAlbum).LoadAsync(cancellationToken);
        }

        return ToDto(entity);
    }

    public async Task<(IEnumerable<GuestLinkDto> Links, int TotalCount)> GetGuestLinksAsync(
        int page = 1,
        int pageSize = 20,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.GuestLinks
            .Include(l => l.CreatedBy)
            .Include(l => l.TargetAlbum)
            .AsQueryable();

        if (!includeExpired)
        {
            query = query.Where(l => l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var links = await query
            .OrderByDescending(l => l.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = links.Select(ToDto);

        return (dtos, totalCount);
    }

    public async Task<GuestLinkDto?> GetGuestLinkByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GuestLinks
            .Include(l => l.CreatedBy)
            .Include(l => l.TargetAlbum)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        return entity != null ? ToDto(entity) : null;
    }

    public async Task<GuestLinkValidationResponse> ValidateGuestLinkAsync(string linkCode, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GuestLinks
            .Include(l => l.TargetAlbum)
            .FirstOrDefaultAsync(l => l.Id == linkCode, cancellationToken);

        if (entity == null)
        {
            return new GuestLinkValidationResponse(
                IsValid: false,
                Name: null,
                TargetAlbumName: null,
                RemainingUploads: null,
                Message: "Invalid or expired link.");
        }

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new GuestLinkValidationResponse(
                IsValid: false,
                Name: entity.Name,
                TargetAlbumName: entity.TargetAlbum?.Name,
                RemainingUploads: null,
                Message: "This link has expired.");
        }

        if (entity.MaxUploads.HasValue && entity.CurrentUploads >= entity.MaxUploads.Value)
        {
            return new GuestLinkValidationResponse(
                IsValid: false,
                Name: entity.Name,
                TargetAlbumName: entity.TargetAlbum?.Name,
                RemainingUploads: 0,
                Message: "This link has reached its upload limit.");
        }

        var remaining = entity.MaxUploads.HasValue
            ? entity.MaxUploads.Value - entity.CurrentUploads
            : (int?)null;

        return new GuestLinkValidationResponse(
            IsValid: true,
            Name: entity.Name,
            TargetAlbumName: entity.TargetAlbum?.Name,
            RemainingUploads: remaining,
            Message: null);
    }

    public async Task<bool> RecordUploadAsync(string linkCode, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GuestLinks
            .FirstOrDefaultAsync(l => l.Id == linkCode, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value < DateTime.UtcNow)
        {
            return false;
        }

        if (entity.MaxUploads.HasValue && entity.CurrentUploads >= entity.MaxUploads.Value)
        {
            return false;
        }

        entity.CurrentUploads++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteGuestLinkAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.GuestLinks
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        _dbContext.GuestLinks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<GuestLinkDto>> GetGuestLinksByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var links = await _dbContext.GuestLinks
            .Include(l => l.CreatedBy)
            .Include(l => l.TargetAlbum)
            .Where(l => l.CreatedById == userId)
            .OrderByDescending(l => l.DateCreated)
            .ToListAsync(cancellationToken);

        return links.Select(ToDto);
    }

    private static GuestLinkDto ToDto(GuestLink entity)
    {
        var isActive = (entity.ExpiresAt == null || entity.ExpiresAt > DateTime.UtcNow) &&
                       (entity.MaxUploads == null || entity.CurrentUploads < entity.MaxUploads);

        return new GuestLinkDto(
            entity.Id,
            entity.Name,
            entity.DateCreated,
            entity.ExpiresAt,
            entity.MaxUploads,
            entity.CurrentUploads,
            entity.TargetAlbumId,
            entity.TargetAlbum?.Name,
            entity.CreatedById,
            entity.CreatedBy.Email,
            isActive);
    }
}
