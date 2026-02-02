using LibraFoto.Modules.Admin.Models;
using LibraFoto.Shared.DTOs;

namespace LibraFoto.Modules.Admin.Services;

/// <summary>
/// Service interface for photo management operations.
/// </summary>
public interface IPhotoService
{
    /// <summary>
    /// Gets a paginated list of photos with optional filtering.
    /// </summary>
    Task<PagedResult<PhotoListDto>> GetPhotosAsync(PhotoFilterRequest filter, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information about a single photo.
    /// </summary>
    Task<PhotoDetailDto?> GetPhotoByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Updates a photo's metadata.
    /// </summary>
    Task<PhotoDetailDto?> UpdatePhotoAsync(long id, UpdatePhotoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a single photo.
    /// </summary>
    Task<bool> DeletePhotoAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Deletes multiple photos.
    /// </summary>
    Task<BulkOperationResult> DeletePhotosAsync(long[] photoIds, CancellationToken ct = default);

    /// <summary>
    /// Adds photos to an album.
    /// </summary>
    Task<BulkOperationResult> AddPhotosToAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default);

    /// <summary>
    /// Removes photos from an album.
    /// </summary>
    Task<BulkOperationResult> RemovePhotosFromAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default);

    /// <summary>
    /// Adds tags to multiple photos.
    /// </summary>
    Task<BulkOperationResult> AddTagsToPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default);

    /// <summary>
    /// Removes tags from multiple photos.
    /// </summary>
    Task<BulkOperationResult> RemoveTagsFromPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default);

    /// <summary>
    /// Gets the total count of photos.
    /// </summary>
    Task<PhotoCountDto> GetPhotoCountAsync(CancellationToken ct = default);
}
