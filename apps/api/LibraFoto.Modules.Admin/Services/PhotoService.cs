using LibraFoto.Modules.Admin.Services.Repositories;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Modules.Media.Services;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Shared.Configuration;
using LibraFoto.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Admin.Services;

/// <summary>
/// Implementation of photo management operations.
/// </summary>
public class PhotoService : IPhotoService
{
    private readonly IPhotoRepository _repository;
    private readonly IThumbnailService _thumbnailService;
    private readonly IStorageProviderFactory _providerFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhotoService> _logger;

    public PhotoService(
        IPhotoRepository repository,
        IThumbnailService thumbnailService,
        IStorageProviderFactory providerFactory,
        IConfiguration configuration,
        ILogger<PhotoService> logger)
    {
        _repository = repository;
        _thumbnailService = thumbnailService;
        _providerFactory = providerFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PagedResult<PhotoListDto>> GetPhotosAsync(PhotoFilterRequest filter, CancellationToken ct = default)
    {
        return await _repository.GetPhotosAsync(filter, ct);
    }

    public async Task<PhotoDetailDto?> GetPhotoByIdAsync(long id, CancellationToken ct = default)
    {
        return await _repository.GetPhotoByIdAsync(id, ct);
    }

    public async Task<PhotoDetailDto?> UpdatePhotoAsync(long id, UpdatePhotoRequest request, CancellationToken ct = default)
    {
        return await _repository.UpdatePhotoAsync(id, request, ct);
    }

    public async Task<bool> DeletePhotoAsync(long id, CancellationToken ct = default)
    {
        var photo = await _repository.GetPhotoDeletionDataAsync(id, ct);
        if (photo is null)
        {
            return false;
        }

        // Collect file paths BEFORE database delete (cascade deletes clear references)
        var filePath = photo.FilePath;
        var thumbnailPath = photo.ThumbnailPath;
        var providerId = photo.ProviderId;
        var providerFileId = photo.ProviderFileId;

        // Start explicit transaction for database operations
        using var transaction = await _repository.BeginTransactionAsync(ct);
        var transactionCommitted = false;

        try
        {
            // Step 1: Delete from database (cascade will handle junction tables)
            var recordDeleted = await _repository.DeletePhotoRecordAsync(id, ct);
            if (!recordDeleted)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            // Step 2: Delete physical files (best-effort; DB deletion remains source of truth)

            // Delete main file from storage provider
            if (providerId.HasValue && !string.IsNullOrEmpty(providerFileId))
            {
                try
                {
                    var provider = await _providerFactory.GetProviderAsync(providerId.Value, ct);
                    if (provider != null)
                    {
                        var fileDeleted = await provider.DeleteFileAsync(providerFileId, ct);
                        if (!fileDeleted)
                        {
                            _logger.LogWarning("Storage provider could not delete file for photo {PhotoId}: {FileId}", id, providerFileId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception deleting file from storage provider for photo {PhotoId}", id);
                }
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                // Local storage - combine relative path with storage root
                try
                {
                    var storagePath = _configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                    var absolutePath = Path.Combine(storagePath, filePath);
                    if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                    else
                    {
                        _logger.LogWarning("Photo file not found for deletion: {Path}", absolutePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception deleting local file for photo {PhotoId}", id);
                }
            }

            // Delete thumbnail (best effort - log but don't fail transaction)
            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                try
                {
                    var thumbnailDeleted = _thumbnailService.DeleteThumbnails(id);
                    if (!thumbnailDeleted)
                    {
                        _logger.LogWarning("Thumbnail not found for deletion for photo {PhotoId}", id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete thumbnail for photo {PhotoId} - continuing anyway", id);
                }
            }

            // Commit transaction
            await transaction.CommitAsync(ct);
            transactionCommitted = true;
            _logger.LogInformation("Successfully deleted photo {PhotoId} with files", id);

            return true;
        }
        catch (Exception)
        {
            // Ensure rollback on any other failure
            if (!transactionCommitted)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
    }

    public async Task<BulkOperationResult> DeletePhotosAsync(long[] photoIds, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var successCount = 0;
        var failureCount = 0;
        const int MaxFailures = 3;

        // Process each deletion individually with transaction rollback on failure
        foreach (var photoId in photoIds)
        {
            // Stop if we've hit the maximum failure threshold
            if (failureCount >= MaxFailures)
            {
                var remainingCount = photoIds.Length - (successCount + failureCount);
                errors.Add($"Stopped after {MaxFailures} failures. {remainingCount} photos not attempted.");
                break;
            }

            try
            {
                var deleted = await DeletePhotoAsync(photoId, ct);
                if (deleted)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"Photo {photoId} not found");
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Photo {photoId} deletion failed: {ex.Message}");
                failureCount++;
                _logger.LogError(ex, "Failed to delete photo {PhotoId} in bulk operation", photoId);
            }
        }

        return new BulkOperationResult(successCount, errors.Count, errors.ToArray());
    }

    public async Task<BulkOperationResult> AddPhotosToAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.AddPhotosToAlbumAsync(albumId, photoIds, ct);
    }

    public async Task<BulkOperationResult> RemovePhotosFromAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.RemovePhotosFromAlbumAsync(albumId, photoIds, ct);
    }

    public async Task<BulkOperationResult> AddTagsToPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default)
    {
        return await _repository.AddTagsToPhotosAsync(photoIds, tagIds, ct);
    }

    public async Task<BulkOperationResult> RemoveTagsFromPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default)
    {
        return await _repository.RemoveTagsFromPhotosAsync(photoIds, tagIds, ct);
    }

    public async Task<PhotoCountDto> GetPhotoCountAsync(CancellationToken ct = default)
    {
        return await _repository.GetPhotoCountAsync(ct);
    }
}
