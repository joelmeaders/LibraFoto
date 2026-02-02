using LibraFoto.Data.Entities;
using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Interfaces;

/// <summary>
/// Service for managing cached files from cloud storage providers.
/// Implements LRU eviction and size limits.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached file by its hash. Updates last accessed date.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cached file info if found, null otherwise.</returns>
    Task<CachedFile?> GetCachedFileAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caches a file downloaded from a storage provider.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file content.</param>
    /// <param name="originalUrl">Original URL from the provider.</param>
    /// <param name="providerId">Storage provider ID.</param>
    /// <param name="fileStream">Stream containing the file data.</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached file record.</returns>
    Task<CachedFile> CacheFileAsync(
        CacheFileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached file by provider and provider-specific file identifier.
    /// </summary>
    /// <param name="providerId">Storage provider ID.</param>
    /// <param name="providerFileId">Provider-specific file identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cached file info if found, null otherwise.</returns>
    Task<CachedFile?> GetCachedFileByProviderFileIdAsync(
        long providerId,
        string providerFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the local file path for a cached file.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File stream if found, null otherwise.</returns>
    Task<Stream?> GetCachedFileStreamAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total size of all cached files in bytes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total cache size in bytes.</returns>
    Task<long> GetCacheSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of cached files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of cached files.</returns>
    Task<int> GetCacheCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears cached files for a specific provider.
    /// </summary>
    /// <param name="providerId">Storage provider ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearProviderCacheAsync(long providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts files using LRU strategy until cache size is below the limit.
    /// </summary>
    /// <param name="maxSizeBytes">Maximum cache size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files evicted.</returns>
    Task<int> EvictLRUAsync(long maxSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific cached file.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCachedFileAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of cached files.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of cached files.</returns>
    Task<(List<CachedFile> Files, int TotalCount)> GetCachedFilesAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
