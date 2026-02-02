using System.Security.Cryptography;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Services;

public class CacheService : ICacheService
{
    private readonly LibraFotoDbContext _dbContext;
    private readonly ILogger<CacheService> _logger;
    private readonly string _cacheDirectory;
    private readonly long _maxCacheSizeBytes;

    public CacheService(
        LibraFotoDbContext dbContext,
        IConfiguration configuration,
        ILogger<CacheService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Get cache settings from configuration
        var cacheDir = configuration["Cache:Directory"];
        _cacheDirectory = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LibraFoto",
            "cache");

        // Default 5GB
        _maxCacheSizeBytes = configuration.GetValue<long>("Cache:MaxSizeBytes", 5L * 1024 * 1024 * 1024);

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<CachedFile?> GetCachedFileAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        var cachedFile = await _dbContext.CachedFiles
            .FirstOrDefaultAsync(f => f.FileHash == fileHash, cancellationToken);

        if (cachedFile != null)
        {
            // Update access tracking
            cachedFile.LastAccessedDate = DateTime.UtcNow;
            cachedFile.AccessCount++;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update access tracking for cached file: {Hash}", fileHash);
            }
        }

        return cachedFile;
    }

    public async Task<Stream?> GetCachedFileStreamAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        var cachedFile = await GetCachedFileAsync(fileHash, cancellationToken);

        if (cachedFile == null || !File.Exists(cachedFile.LocalPath))
        {
            return null;
        }

        return new FileStream(cachedFile.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
    }

    public async Task<CachedFile?> GetCachedFileByProviderFileIdAsync(
        long providerId,
        string providerFileId,
        CancellationToken cancellationToken = default)
    {
        var cachedFile = await _dbContext.CachedFiles
            .FirstOrDefaultAsync(f => f.ProviderId == providerId && f.ProviderFileId == providerFileId, cancellationToken);

        if (cachedFile != null)
        {
            cachedFile.LastAccessedDate = DateTime.UtcNow;
            cachedFile.AccessCount++;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update access tracking for cached file: {Hash}", cachedFile.FileHash);
            }
        }

        return cachedFile;
    }

    public async Task<CachedFile> CacheFileAsync(
        CacheFileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if already cached
        var existing = await _dbContext.CachedFiles
            .FirstOrDefaultAsync(f => f.FileHash == request.FileHash, cancellationToken);

        if (existing != null)
        {
            _logger.LogDebug("File already cached: {Hash}", request.FileHash);
            if (!string.IsNullOrWhiteSpace(request.ProviderFileId) && string.IsNullOrWhiteSpace(existing.ProviderFileId))
            {
                existing.ProviderFileId = request.ProviderFileId;
                existing.PickerSessionId ??= request.PickerSessionId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return existing;
        }

        // Determine file extension from content type
        var extension = GetExtensionFromContentType(request.ContentType);

        // Build local path using hash-based directory structure
        var subDir1 = request.FileHash[..2];
        var subDir2 = request.FileHash.Substring(2, 2);
        var directory = Path.Combine(_cacheDirectory, subDir1, subDir2);
        Directory.CreateDirectory(directory);

        var localPath = Path.Combine(directory, $"{request.FileHash}{extension}");

        // Write file to disk
        long fileSize;
        using (var fileOut = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await request.FileStream.CopyToAsync(fileOut, cancellationToken);
            fileSize = fileOut.Length;
        }

        // Create database record
        var cachedFile = new CachedFile
        {
            FileHash = request.FileHash,
            OriginalUrl = request.OriginalUrl,
            ProviderId = request.ProviderId,
            ProviderFileId = request.ProviderFileId,
            PickerSessionId = request.PickerSessionId,
            LocalPath = localPath,
            FileSize = fileSize,
            ContentType = request.ContentType,
            CachedDate = DateTime.UtcNow,
            LastAccessedDate = DateTime.UtcNow,
            AccessCount = 1
        };

        _dbContext.CachedFiles.Add(cachedFile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cached file: {Hash} ({Size} bytes)", request.FileHash, fileSize);

        // Check cache size and evict if needed
        var currentSize = await GetCacheSizeAsync(cancellationToken);
        if (currentSize > _maxCacheSizeBytes)
        {
            _logger.LogInformation("Cache size ({Current}) exceeds limit ({Max}), evicting...",
                currentSize, _maxCacheSizeBytes);
            await EvictLRUAsync(_maxCacheSizeBytes, cancellationToken);
        }

        return cachedFile;
    }

    public async Task<long> GetCacheSizeAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CachedFiles
            .SumAsync(f => (long?)f.FileSize, cancellationToken) ?? 0;
    }

    public async Task<int> GetCacheCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CachedFiles.CountAsync(cancellationToken);
    }

    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        var files = await _dbContext.CachedFiles.ToListAsync(cancellationToken);

        foreach (var path in files.Select(file => file.LocalPath))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cached file: {Path}", path);
            }
        }

        _dbContext.CachedFiles.RemoveRange(files);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared cache: {Count} files deleted", files.Count);
    }

    public async Task ClearProviderCacheAsync(long providerId, CancellationToken cancellationToken = default)
    {
        var files = await _dbContext.CachedFiles
            .Where(f => f.ProviderId == providerId)
            .ToListAsync(cancellationToken);

        foreach (var path in files.Select(file => file.LocalPath))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cached file: {Path}", path);
            }
        }

        _dbContext.CachedFiles.RemoveRange(files);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared provider {ProviderId} cache: {Count} files", providerId, files.Count);
    }

    public async Task<int> EvictLRUAsync(long maxSizeBytes, CancellationToken cancellationToken = default)
    {
        var currentSize = await GetCacheSizeAsync(cancellationToken);

        if (currentSize <= maxSizeBytes)
        {
            return 0;
        }

        var targetSize = (long)(maxSizeBytes * 0.8); // Evict to 80% of limit
        var toEvict = currentSize - targetSize;

        // Get files ordered by LRU (oldest access first)
        var files = await _dbContext.CachedFiles
            .OrderBy(f => f.LastAccessedDate)
            .ToListAsync(cancellationToken);

        var evicted = 0;
        long freedSpace = 0;

        foreach (var file in files)
        {
            if (freedSpace >= toEvict)
            {
                break;
            }

            try
            {
                if (File.Exists(file.LocalPath))
                {
                    File.Delete(file.LocalPath);
                }

                _dbContext.CachedFiles.Remove(file);
                freedSpace += file.FileSize;
                evicted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evict cached file: {Path}", file.LocalPath);
            }
        }

        if (evicted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Evicted {Count} files, freed {Size} bytes", evicted, freedSpace);
        }

        return evicted;
    }

    public async Task DeleteCachedFileAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        var cachedFile = await _dbContext.CachedFiles
            .FirstOrDefaultAsync(f => f.FileHash == fileHash, cancellationToken);

        if (cachedFile == null)
        {
            return;
        }

        try
        {
            if (File.Exists(cachedFile.LocalPath))
            {
                File.Delete(cachedFile.LocalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete cached file: {Path}", cachedFile.LocalPath);
        }

        _dbContext.CachedFiles.Remove(cachedFile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<CachedFile> Files, int TotalCount)> GetCachedFilesAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CachedFiles
            .Include(f => f.Provider)
            .OrderByDescending(f => f.LastAccessedDate);

        var totalCount = await query.CountAsync(cancellationToken);

        var files = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (files, totalCount);
    }

    /// <summary>
    /// Computes SHA256 hash of a stream.
    /// </summary>
    public static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "video/mp4" => ".mp4",
            "video/mpeg" => ".mpeg",
            "video/quicktime" => ".mov",
            "video/x-msvideo" => ".avi",
            _ => ".bin"
        };
    }
}
