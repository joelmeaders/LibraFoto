using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibraFoto.Modules.Admin.Services.Repositories;

/// <summary>
/// Data-access operations for admin photo workflows.
/// </summary>
public interface IPhotoRepository
{
    Task<PagedResult<PhotoListDto>> GetPhotosAsync(PhotoFilterRequest filter, CancellationToken ct = default);
    Task<PhotoDetailDto?> GetPhotoByIdAsync(long id, CancellationToken ct = default);
    Task<PhotoDetailDto?> UpdatePhotoAsync(long id, UpdatePhotoRequest request, CancellationToken ct = default);
    Task<PhotoDeletionData?> GetPhotoDeletionDataAsync(long id, CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<bool> DeletePhotoRecordAsync(long id, CancellationToken ct = default);
    Task<BulkOperationResult> AddPhotosToAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default);
    Task<BulkOperationResult> RemovePhotosFromAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default);
    Task<BulkOperationResult> AddTagsToPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default);
    Task<BulkOperationResult> RemoveTagsFromPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default);
    Task<PhotoCountDto> GetPhotoCountAsync(CancellationToken ct = default);
}

/// <summary>
/// Projection of data needed by photo file-deletion orchestration.
/// </summary>
/// <param name="Id">Photo id.</param>
/// <param name="FilePath">Relative file path.</param>
/// <param name="ThumbnailPath">Relative thumbnail path.</param>
/// <param name="ProviderId">Storage provider id.</param>
/// <param name="ProviderFileId">Provider-specific file id.</param>
public sealed record PhotoDeletionData(
    long Id,
    string? FilePath,
    string? ThumbnailPath,
    long? ProviderId,
    string? ProviderFileId);
