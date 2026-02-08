namespace LibraFoto.Modules.Storage.Models
{
    /// <summary>
    /// Result of a sync operation.
    /// </summary>
    public record SyncResult
    {
        /// <summary>
        /// Database ID of the storage provider that was synced.
        /// </summary>
        public long ProviderId { get; init; }

        /// <summary>
        /// Name of the storage provider.
        /// </summary>
        public string ProviderName { get; init; } = string.Empty;

        /// <summary>
        /// Whether the sync completed successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Error message if the sync failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Number of new files added.
        /// </summary>
        public int FilesAdded { get; init; }

        /// <summary>
        /// Number of existing files updated.
        /// </summary>
        public int FilesUpdated { get; init; }

        /// <summary>
        /// Number of files removed (no longer exist in provider).
        /// </summary>
        public int FilesRemoved { get; init; }

        /// <summary>
        /// Number of files skipped (already exist, errors, etc.).
        /// </summary>
        public int FilesSkipped { get; init; }

        /// <summary>
        /// Total number of files processed.
        /// </summary>
        public int TotalFilesProcessed { get; init; }

        /// <summary>
        /// Total number of files found in the provider.
        /// </summary>
        public int TotalFilesFound { get; init; }

        /// <summary>
        /// Time when the sync started.
        /// </summary>
        public DateTime StartTime { get; init; }

        /// <summary>
        /// Time when the sync completed.
        /// </summary>
        public DateTime EndTime { get; init; }

        /// <summary>
        /// Duration of the sync operation.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// List of errors that occurred during sync (for partial failures).
        /// </summary>
        public List<string> Errors { get; init; } = [];

        /// <summary>
        /// Creates a successful sync result.
        /// </summary>
        public static SyncResult Successful(long providerId, string providerName, int added, int updated, int removed, int skipped, int total, DateTime start) =>
            new()
            {
                ProviderId = providerId,
                ProviderName = providerName,
                Success = true,
                FilesAdded = added,
                FilesUpdated = updated,
                FilesRemoved = removed,
                FilesSkipped = skipped,
                TotalFilesProcessed = added + updated + removed + skipped,
                TotalFilesFound = total,
                StartTime = start,
                EndTime = DateTime.UtcNow
            };

        /// <summary>
        /// Creates a failed sync result.
        /// </summary>
        public static SyncResult Failed(long providerId, string providerName, string errorMessage, DateTime start) =>
            new()
            {
                ProviderId = providerId,
                ProviderName = providerName,
                Success = false,
                ErrorMessage = errorMessage,
                StartTime = start,
                EndTime = DateTime.UtcNow
            };
    }

    /// <summary>
    /// Current status of a sync operation.
    /// </summary>
    public record SyncStatus
    {
        /// <summary>
        /// Database ID of the storage provider.
        /// </summary>
        public long ProviderId { get; init; }

        /// <summary>
        /// Whether a sync is currently in progress.
        /// </summary>
        public bool IsInProgress { get; init; }

        /// <summary>
        /// Current sync progress (0-100).
        /// </summary>
        public int ProgressPercent { get; init; }

        /// <summary>
        /// Current operation being performed.
        /// </summary>
        public string? CurrentOperation { get; init; }

        /// <summary>
        /// Number of files processed so far.
        /// </summary>
        public int FilesProcessed { get; init; }

        /// <summary>
        /// Total number of files to process (if known).
        /// </summary>
        public int? TotalFiles { get; init; }

        /// <summary>
        /// Time when the current sync started.
        /// </summary>
        public DateTime? StartTime { get; init; }

        /// <summary>
        /// Result of the last completed sync.
        /// </summary>
        public SyncResult? LastSyncResult { get; init; }
    }

    /// <summary>
    /// Result of scanning a provider for files without importing.
    /// </summary>
    public record ScanResult
    {
        /// <summary>
        /// Database ID of the storage provider.
        /// </summary>
        public long ProviderId { get; init; }

        /// <summary>
        /// Whether the scan completed successfully.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Error message if the scan failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Total number of media files found.
        /// </summary>
        public int TotalFilesFound { get; init; }

        /// <summary>
        /// Number of new files (not yet in database).
        /// </summary>
        public int NewFilesCount { get; init; }

        /// <summary>
        /// Number of files already in database.
        /// </summary>
        public int ExistingFilesCount { get; init; }

        /// <summary>
        /// Total size of new files in bytes.
        /// </summary>
        public long NewFilesTotalSize { get; init; }

        /// <summary>
        /// Sample of new files found (for preview).
        /// </summary>
        public List<StorageFileInfo> SampleNewFiles { get; init; } = [];
    }
}
