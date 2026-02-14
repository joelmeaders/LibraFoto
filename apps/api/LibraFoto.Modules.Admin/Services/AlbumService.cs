using LibraFoto.Modules.Admin.Services.Repositories;
using LibraFoto.Modules.Admin.Services.Shared;

namespace LibraFoto.Modules.Admin.Services;

/// <summary>
/// Implementation of album management operations.
/// </summary>
public class AlbumService : IAlbumService
{
    private readonly IAlbumRepository _repository;

    public AlbumService(IAlbumRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AlbumDto>> GetAlbumsAsync(CancellationToken ct = default)
    {
        return await _repository.GetAlbumsAsync(ct);
    }

    public async Task<AlbumDto?> GetAlbumByIdAsync(long id, CancellationToken ct = default)
    {
        return await _repository.GetAlbumByIdAsync(id, ct);
    }

    public async Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default)
    {
        return await _repository.CreateAlbumAsync(request, ct);
    }

    public async Task<AlbumDto?> UpdateAlbumAsync(long id, UpdateAlbumRequest request, CancellationToken ct = default)
    {
        return await _repository.UpdateAlbumAsync(id, request, ct);
    }

    public async Task<bool> DeleteAlbumAsync(long id, CancellationToken ct = default)
    {
        return await _repository.DeleteAlbumAsync(id, ct);
    }

    public async Task<AlbumDto?> SetCoverPhotoAsync(long albumId, long photoId, CancellationToken ct = default)
    {
        return await _repository.SetCoverPhotoAsync(albumId, photoId, ct);
    }

    public async Task<AlbumDto?> RemoveCoverPhotoAsync(long albumId, CancellationToken ct = default)
    {
        return await _repository.RemoveCoverPhotoAsync(albumId, ct);
    }

    public async Task<BulkOperationResult> AddPhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.AddPhotosAsync(albumId, photoIds, ct);
    }

    public async Task<BulkOperationResult> RemovePhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.RemovePhotosAsync(albumId, photoIds, ct);
    }

    public async Task<bool> ReorderPhotosAsync(long albumId, PhotoOrder[] orders, CancellationToken ct = default)
    {
        return await _repository.ReorderPhotosAsync(albumId, orders, ct);
    }
}
