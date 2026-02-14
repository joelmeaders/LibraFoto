using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Storage.Services.Repositories;

/// <summary>
/// Entity Framework implementation of storage persistence repository.
/// </summary>
public sealed class StoragePersistenceRepository : IStoragePersistenceRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public StoragePersistenceRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<StorageProvider?> GetEnabledProviderByIdAsync(long providerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId && p.IsEnabled, cancellationToken);
    }

    public Task<List<StorageProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<List<StorageProvider>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public Task<List<StorageProvider>> GetEnabledProvidersByTypeAsync(StorageProviderType type, CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders
            .AsNoTracking()
            .Where(p => p.Type == type && p.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public Task<StorageProvider?> GetProviderByTypeAsync(StorageProviderType type, CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders
            .FirstOrDefaultAsync(p => p.Type == type, cancellationToken);
    }

    public Task AddProviderAsync(StorageProvider provider, CancellationToken cancellationToken = default)
    {
        _dbContext.StorageProviders.Add(provider);
        return Task.CompletedTask;
    }

    public Task RemoveProviderAsync(StorageProvider provider, CancellationToken cancellationToken = default)
    {
        _dbContext.StorageProviders.Remove(provider);
        return Task.CompletedTask;
    }

    public Task<HashSet<string>> GetProviderPhotoFileIdsAsync(long providerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Photos
            .Where(p => p.ProviderId == providerId)
            .Where(p => p.ProviderFileId != null)
            .Select(p => p.ProviderFileId!)
            .ToHashSetAsync(cancellationToken);
    }

    public Task<Photo?> GetPhotoByProviderFileIdAsync(long providerId, string providerFileId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Photos
            .FirstOrDefaultAsync(p => p.ProviderId == providerId && p.ProviderFileId == providerFileId, cancellationToken);
    }

    public Task AddPhotoAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        _dbContext.Photos.Add(photo);
        return Task.CompletedTask;
    }

    public Task RemovePhotoAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        _dbContext.Photos.Remove(photo);
        return Task.CompletedTask;
    }

    public Task<List<Photo>> GetPhotosByProviderAndFileIdsAsync(long providerId, IEnumerable<string> providerFileIds, CancellationToken cancellationToken = default)
    {
        var fileIds = providerFileIds.ToList();
        return _dbContext.Photos
            .Where(p => p.ProviderId == providerId && p.ProviderFileId != null && fileIds.Contains(p.ProviderFileId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Photo>> GetPhotosByProviderAsync(long providerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Photos
            .Where(p => p.ProviderId == providerId)
            .OrderByDescending(p => p.DateTaken ?? p.DateAdded)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountPhotosByProviderAsync(long providerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Photos.CountAsync(p => p.ProviderId == providerId, cancellationToken);
    }

    public Task RemovePhotosAsync(IEnumerable<Photo> photos)
    {
        _dbContext.Photos.RemoveRange(photos);
        return Task.CompletedTask;
    }

    public Task<Album?> GetAlbumByIdAsync(long albumId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Albums.FindAsync([albumId], cancellationToken).AsTask();
    }

    public Task AddPhotoAlbumAsync(PhotoAlbum photoAlbum, CancellationToken cancellationToken = default)
    {
        _dbContext.PhotoAlbums.Add(photoAlbum);
        return Task.CompletedTask;
    }

    public Task<GuestLink?> GetGuestLinkByIdAsync(string linkId, CancellationToken cancellationToken = default)
    {
        return _dbContext.GuestLinks.FirstOrDefaultAsync(g => g.Id == linkId, cancellationToken);
    }

    public Task<PickerSession?> GetPickerSessionAsync(long providerId, string sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PickerSessions
            .FirstOrDefaultAsync(s => s.ProviderId == providerId && s.SessionId == sessionId, cancellationToken);
    }

    public Task AddPickerSessionAsync(PickerSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.PickerSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task RemovePickerSessionAsync(PickerSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.PickerSessions.Remove(session);
        return Task.CompletedTask;
    }

    public Task<StorageProvider?> GetProviderByIdAsync(long providerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.StorageProviders.FindAsync([providerId], cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
