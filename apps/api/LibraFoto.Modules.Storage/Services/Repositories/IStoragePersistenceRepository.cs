using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Services.Repositories;

/// <summary>
/// Repository abstraction for storage module persistence operations.
/// </summary>
public interface IStoragePersistenceRepository
{
    Task<StorageProvider?> GetEnabledProviderByIdAsync(long providerId, CancellationToken cancellationToken = default);
    Task<List<StorageProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default);
    Task<List<StorageProvider>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default);
    Task<List<StorageProvider>> GetEnabledProvidersByTypeAsync(StorageProviderType type, CancellationToken cancellationToken = default);
    Task<StorageProvider?> GetProviderByTypeAsync(StorageProviderType type, CancellationToken cancellationToken = default);
    Task AddProviderAsync(StorageProvider provider, CancellationToken cancellationToken = default);
    Task RemoveProviderAsync(StorageProvider provider, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetProviderPhotoFileIdsAsync(long providerId, CancellationToken cancellationToken = default);
    Task<Photo?> GetPhotoByProviderFileIdAsync(long providerId, string providerFileId, CancellationToken cancellationToken = default);
    Task AddPhotoAsync(Photo photo, CancellationToken cancellationToken = default);
    Task RemovePhotoAsync(Photo photo, CancellationToken cancellationToken = default);
    Task<List<Photo>> GetPhotosByProviderAndFileIdsAsync(long providerId, IEnumerable<string> providerFileIds, CancellationToken cancellationToken = default);
    Task<List<Photo>> GetPhotosByProviderAsync(long providerId, CancellationToken cancellationToken = default);
    Task<int> CountPhotosByProviderAsync(long providerId, CancellationToken cancellationToken = default);
    Task RemovePhotosAsync(IEnumerable<Photo> photos);

    Task<Album?> GetAlbumByIdAsync(long albumId, CancellationToken cancellationToken = default);
    Task AddPhotoAlbumAsync(PhotoAlbum photoAlbum, CancellationToken cancellationToken = default);

    Task<GuestLink?> GetGuestLinkByIdAsync(string linkId, CancellationToken cancellationToken = default);

    Task<PickerSession?> GetPickerSessionAsync(long providerId, string sessionId, CancellationToken cancellationToken = default);
    Task AddPickerSessionAsync(PickerSession session, CancellationToken cancellationToken = default);
    Task RemovePickerSessionAsync(PickerSession session, CancellationToken cancellationToken = default);

    Task<StorageProvider?> GetProviderByIdAsync(long providerId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
