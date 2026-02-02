using System.Collections.Concurrent;
using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Storage.Interfaces;
using LibraFoto.Modules.Storage.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Storage.Services;

/// <summary>
/// Service for synchronizing files between storage providers and the local database.
/// </summary>
public class SyncService : ISyncService
{
    private readonly IStorageProviderFactory _providerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncService> _logger;

    // Track active syncs and their cancellation tokens
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _activeSyncs = new();
    private readonly ConcurrentDictionary<long, SyncStatus> _syncStatuses = new();
    private readonly ConcurrentDictionary<long, SyncResult> _lastSyncResults = new();

    public SyncService(
        IStorageProviderFactory providerFactory,
        IServiceProvider serviceProvider,
        ILogger<SyncService> logger)
    {
        _providerFactory = providerFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SyncResult> SyncProviderAsync(
        long providerId,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Check if sync is already in progress
        if (_activeSyncs.ContainsKey(providerId))
        {
            return SyncResult.Failed(providerId, "", "A sync is already in progress for this provider", startTime);
        }

        var provider = await _providerFactory.GetProviderAsync(providerId, cancellationToken);
        if (provider == null)
        {
            return SyncResult.Failed(providerId, "", "Storage provider not found or disabled", startTime);
        }

        // Create a linked cancellation token
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_activeSyncs.TryAdd(providerId, cts))
        {
            return SyncResult.Failed(providerId, provider.DisplayName, "Failed to start sync", startTime);
        }

        try
        {
            _logger.LogInformation("Starting sync for provider {ProviderId} ({Name})", providerId, provider.DisplayName);

            UpdateSyncStatus(providerId, new SyncStatus
            {
                ProviderId = providerId,
                IsInProgress = true,
                ProgressPercent = 0,
                CurrentOperation = "Scanning files...",
                StartTime = startTime
            });

            // Get files from provider
            var files = await provider.GetFilesAsync(request.FolderId, cts.Token);
            var fileList = files.Where(f => !f.IsFolder).ToList();
            var totalFiles = fileList.Count;

            UpdateSyncStatus(providerId, new SyncStatus
            {
                ProviderId = providerId,
                IsInProgress = true,
                ProgressPercent = 10,
                CurrentOperation = $"Found {totalFiles} files, processing...",
                TotalFiles = totalFiles,
                StartTime = startTime
            });

            var added = 0;
            var updated = 0;
            var removed = 0;
            var skipped = 0;
            var errors = new List<string>();

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            // Get existing file IDs for this provider
            var existingFileIds = await dbContext.Photos
                .Where(p => p.ProviderId == providerId)
                .Select(p => p.ProviderFileId)
                .ToHashSetAsync(cts.Token);

            var processedFileIds = new HashSet<string>();
            var processed = 0;

            foreach (var file in fileList)
            {
                cts.Token.ThrowIfCancellationRequested();

                try
                {
                    processedFileIds.Add(file.FileId);

                    if (existingFileIds.Contains(file.FileId))
                    {
                        if (request.SkipExisting)
                        {
                            skipped++;
                        }
                        else
                        {
                            // Update existing record
                            var existingPhoto = await dbContext.Photos
                                .FirstOrDefaultAsync(p => p.ProviderId == providerId && p.ProviderFileId == file.FileId, cts.Token);

                            if (existingPhoto != null)
                            {
                                existingPhoto.FileSize = file.FileSize;
                                existingPhoto.Width = file.Width ?? 0;
                                existingPhoto.Height = file.Height ?? 0;
                                updated++;
                            }
                        }
                    }
                    else
                    {
                        // Add new record
                        var photo = new Photo
                        {
                            Filename = file.FileName,
                            OriginalFilename = file.FileName,
                            FilePath = file.FullPath ?? file.FileId,
                            FileSize = file.FileSize,
                            Width = file.Width ?? 0,
                            Height = file.Height ?? 0,
                            MediaType = file.MediaType,
                            Duration = file.Duration,
                            DateTaken = file.CreatedDate,
                            DateAdded = DateTime.UtcNow,
                            ProviderId = providerId,
                            ProviderFileId = file.FileId
                        };

                        dbContext.Photos.Add(photo);
                        added++;
                    }

                    processed++;

                    // Update progress every 10 files or at the end
                    if (processed % 10 == 0 || processed == totalFiles)
                    {
                        var percent = 10 + (int)(80.0 * processed / totalFiles);
                        UpdateSyncStatus(providerId, new SyncStatus
                        {
                            ProviderId = providerId,
                            IsInProgress = true,
                            ProgressPercent = percent,
                            CurrentOperation = $"Processing files ({processed}/{totalFiles})...",
                            FilesProcessed = processed,
                            TotalFiles = totalFiles,
                            StartTime = startTime
                        });

                        // Save in batches
                        await dbContext.SaveChangesAsync(cts.Token);
                    }

                    // Limit files if requested
                    if (request.MaxFiles > 0 && processed >= request.MaxFiles)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error processing {file.FileName}: {ex.Message}");
                    _logger.LogWarning(ex, "Error processing file {FileName} during sync", file.FileName);
                }
            }

            // Handle deleted files
            if (request.RemoveDeleted)
            {
                UpdateSyncStatus(providerId, new SyncStatus
                {
                    ProviderId = providerId,
                    IsInProgress = true,
                    ProgressPercent = 95,
                    CurrentOperation = "Checking for deleted files...",
                    FilesProcessed = processed,
                    TotalFiles = totalFiles,
                    StartTime = startTime
                });

                var deletedFileIds = existingFileIds.Except(processedFileIds).ToList();
                if (deletedFileIds.Count > 0)
                {
                    var photosToRemove = await dbContext.Photos
                        .Where(p => p.ProviderId == providerId && deletedFileIds.Contains(p.ProviderFileId))
                        .ToListAsync(cts.Token);

                    dbContext.Photos.RemoveRange(photosToRemove);
                    removed = photosToRemove.Count;
                }
            }

            // Final save
            await dbContext.SaveChangesAsync(cts.Token);

            // Update provider's last sync date
            var providerEntity = await dbContext.StorageProviders.FindAsync([providerId], cts.Token);
            if (providerEntity != null)
            {
                providerEntity.LastSyncDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cts.Token);
            }

            var result = SyncResult.Successful(providerId, provider.DisplayName, added, updated, removed, skipped, totalFiles, startTime);
            if (errors.Count > 0)
            {
                result = result with { Errors = errors };
            }

            _lastSyncResults[providerId] = result;
            _logger.LogInformation(
                "Sync completed for provider {ProviderId}: {Added} added, {Updated} updated, {Removed} removed, {Skipped} skipped",
                providerId, added, updated, removed, skipped);

            return result;
        }
        catch (OperationCanceledException)
        {
            var result = SyncResult.Failed(providerId, provider.DisplayName, "Sync was cancelled", startTime);
            _lastSyncResults[providerId] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing provider {ProviderId}", providerId);
            var result = SyncResult.Failed(providerId, provider.DisplayName, ex.Message, startTime);
            _lastSyncResults[providerId] = result;
            return result;
        }
        finally
        {
            _activeSyncs.TryRemove(providerId, out var cts2);
            cts2?.Dispose();

            UpdateSyncStatus(providerId, new SyncStatus
            {
                ProviderId = providerId,
                IsInProgress = false,
                ProgressPercent = 100,
                LastSyncResult = _lastSyncResults.GetValueOrDefault(providerId)
            });
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SyncResult>> SyncAllProvidersAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var providers = await _providerFactory.GetAllProvidersAsync(cancellationToken);
        var results = new List<SyncResult>();

        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await SyncProviderAsync(provider.ProviderId, request, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public Task<SyncStatus> GetSyncStatusAsync(long providerId, CancellationToken cancellationToken = default)
    {
        if (_syncStatuses.TryGetValue(providerId, out var status))
        {
            return Task.FromResult(status);
        }

        return Task.FromResult(new SyncStatus
        {
            ProviderId = providerId,
            IsInProgress = false,
            LastSyncResult = _lastSyncResults.GetValueOrDefault(providerId)
        });
    }

    /// <inheritdoc />
    public bool CancelSync(long providerId)
    {
        if (_activeSyncs.TryRemove(providerId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<ScanResult> ScanProviderAsync(long providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providerFactory.GetProviderAsync(providerId, cancellationToken);
        if (provider == null)
        {
            return new ScanResult
            {
                ProviderId = providerId,
                Success = false,
                ErrorMessage = "Storage provider not found or disabled"
            };
        }

        try
        {
            var files = await provider.GetFilesAsync(null, cancellationToken);
            var fileList = files.Where(f => !f.IsFolder).ToList();

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

            var existingFileIds = await dbContext.Photos
                .Where(p => p.ProviderId == providerId)
                .Select(p => p.ProviderFileId)
                .ToHashSetAsync(cancellationToken);

            var newFiles = fileList.Where(f => !existingFileIds.Contains(f.FileId)).ToList();
            var newTotalSize = newFiles.Sum(f => f.FileSize);

            return new ScanResult
            {
                ProviderId = providerId,
                Success = true,
                TotalFilesFound = fileList.Count,
                NewFilesCount = newFiles.Count,
                ExistingFilesCount = fileList.Count - newFiles.Count,
                NewFilesTotalSize = newTotalSize,
                SampleNewFiles = newFiles.Take(10).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning provider {ProviderId}", providerId);
            return new ScanResult
            {
                ProviderId = providerId,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private void UpdateSyncStatus(long providerId, SyncStatus status)
    {
        _syncStatuses[providerId] = status;
    }
}
