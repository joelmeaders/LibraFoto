using System.Text.Json;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Providers;

/// <summary>
/// Storage provider for local file system storage.
/// </summary>
public class LocalStorageProvider : IStorageProvider
{
    private readonly IMediaScannerService _mediaScanner;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalStorageProvider> _logger;

    private long _providerId;
    private string _displayName = "Local Storage";
    private LocalStorageConfiguration _config = new();

    public LocalStorageProvider(
        IMediaScannerService mediaScanner,
        IConfiguration configuration,
        ILogger<LocalStorageProvider> logger)
    {
        _mediaScanner = mediaScanner;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public long ProviderId => _providerId;

    /// <inheritdoc />
    public StorageProviderType ProviderType => StorageProviderType.Local;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <inheritdoc />
    public bool SupportsUpload => true;

    /// <inheritdoc />
    public bool SupportsWatch => true;

    /// <summary>
    /// Gets the base path for local storage.
    /// </summary>
    public string BasePath => _config.BasePath;

    /// <inheritdoc />
    public void Initialize(long providerId, string displayName, string? configuration)
    {
        _providerId = providerId;
        _displayName = displayName;

        if (!string.IsNullOrEmpty(configuration))
        {
            try
            {
                _config = JsonSerializer.Deserialize<LocalStorageConfiguration>(configuration) ?? new();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse local storage configuration, using defaults");
                _config = new LocalStorageConfiguration();
            }
        }

        // Ensure base path exists
        EnsureDirectoryExists(_config.BasePath);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StorageFileInfo>> GetFilesAsync(
        string? folderId,
        CancellationToken cancellationToken = default)
    {
        var targetPath = string.IsNullOrEmpty(folderId)
            ? _config.BasePath
            : Path.Combine(_config.BasePath, folderId);

        if (!Directory.Exists(targetPath))
        {
            _logger.LogWarning("Directory does not exist: {Path}", targetPath);
            return [];
        }

        var scannedFiles = await _mediaScanner.ScanDirectoryAsync(targetPath, recursive: true, cancellationToken);
        var filteredFiles = scannedFiles.Where(f => !IsThumbnailPath(f.RelativePath));

        return filteredFiles.Select(f => new StorageFileInfo
        {
            FileId = f.RelativePath.Replace('\\', '/'), // Normalize path separators
            FileName = f.FileName,
            FullPath = f.FullPath,
            FileSize = f.FileSize,
            ContentType = f.ContentType,
            MediaType = f.MediaType,
            CreatedDate = f.CreatedTime,
            ModifiedDate = f.ModifiedTime,
            IsFolder = false,
            ParentFolderId = Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/')
        });
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var filePath = GetAbsolutePath(fileId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {fileId}", fileId);
        }

        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GetFileStreamAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var filePath = GetAbsolutePath(fileId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {fileId}", fileId);
        }

        // Return a FileStream - caller is responsible for disposing
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public async Task<UploadResult> UploadFileAsync(
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate content type
            if (!_mediaScanner.IsSupportedMediaFile(fileName))
            {
                return UploadResult.Failed($"Unsupported file type: {Path.GetExtension(fileName)}");
            }

            // Determine target directory (organized by date if configured)
            var targetDir = _config.BasePath;
            var relativeDir = "";

            if (_config.OrganizeByDate)
            {
                var now = DateTime.UtcNow;
                relativeDir = Path.Combine(now.Year.ToString(), now.Month.ToString("D2"));
                targetDir = Path.Combine(_config.BasePath, relativeDir);
            }

            EnsureDirectoryExists(targetDir);

            // Generate unique filename
            var uniqueFilename = _mediaScanner.GenerateUniqueFilename(fileName, targetDir);
            var fullPath = Path.Combine(targetDir, uniqueFilename);
            var relativePath = string.IsNullOrEmpty(relativeDir)
                ? uniqueFilename
                : Path.Combine(relativeDir, uniqueFilename).Replace('\\', '/');

            // Write file
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            var fileInfo = new FileInfo(fullPath);

            _logger.LogInformation("Uploaded file {FileName} to {Path}", uniqueFilename, relativePath);

            return new UploadResult
            {
                Success = true,
                FileId = relativePath,
                FileName = uniqueFilename,
                FilePath = relativePath,
                FileSize = fileInfo.Length,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", fileName);
            return UploadResult.Failed($"Upload failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var filePath = GetAbsolutePath(fileId);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted file {FileId}", fileId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", fileId);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var filePath = GetAbsolutePath(fileId);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <inheritdoc />
    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // For local storage, check if the directory exists and is accessible
            if (!Directory.Exists(_config.BasePath))
            {
                EnsureDirectoryExists(_config.BasePath);
            }

            // Try to list the directory to verify read access
            Directory.GetFiles(_config.BasePath, "*", SearchOption.TopDirectoryOnly);

            // Try to create a test file to verify write access
            var testFile = Path.Combine(_config.BasePath, $".librafoto-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local storage connection test failed for {Path}", _config.BasePath);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Gets the absolute file path for a file ID.
    /// </summary>
    private string GetAbsolutePath(string fileId)
    {
        // Normalize and sanitize the file ID
        var normalizedId = fileId.Replace('/', Path.DirectorySeparatorChar)
                                  .Replace('\\', Path.DirectorySeparatorChar);

        // Security check - prevent path traversal
        var fullPath = Path.GetFullPath(Path.Combine(_config.BasePath, normalizedId));
        var basePath = Path.GetFullPath(_config.BasePath);

        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access to path outside storage root is not allowed");
        }

        return fullPath;
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Created directory: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create directory: {Path}", path);
                throw;
            }
        }
    }

    private static bool IsThumbnailPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');

        return normalized.Equals(".thumbnails", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".thumbnails/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.thumbnails/", StringComparison.OrdinalIgnoreCase);
    }
}
