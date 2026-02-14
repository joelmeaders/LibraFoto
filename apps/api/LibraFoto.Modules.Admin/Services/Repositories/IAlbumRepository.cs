using LibraFoto.Modules.Admin.Services.Shared;

namespace LibraFoto.Modules.Admin.Services.Repositories;

/// <summary>
/// Data-access operations for albums.
/// </summary>
public interface IAlbumRepository
{
    Task<IReadOnlyList<AlbumDto>> GetAlbumsAsync(CancellationToken ct = default);
    Task<AlbumDto?> GetAlbumByIdAsync(long id, CancellationToken ct = default);
    Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default);
    Task<AlbumDto?> UpdateAlbumAsync(long id, UpdateAlbumRequest request, CancellationToken ct = default);
    Task<bool> DeleteAlbumAsync(long id, CancellationToken ct = default);
    Task<AlbumDto?> SetCoverPhotoAsync(long albumId, long photoId, CancellationToken ct = default);
    Task<AlbumDto?> RemoveCoverPhotoAsync(long albumId, CancellationToken ct = default);
    Task<BulkOperationResult> AddPhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default);
    Task<BulkOperationResult> RemovePhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default);
    Task<bool> ReorderPhotosAsync(long albumId, PhotoOrder[] orders, CancellationToken ct = default);
}
