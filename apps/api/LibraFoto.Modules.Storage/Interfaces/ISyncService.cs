using LibraFoto.Modules.Storage.Models;

namespace LibraFoto.Modules.Storage.Interfaces
{
    /// <summary>
    /// Service for synchronizing files between storage providers and the local database.
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Performs a full sync for a specific storage provider.
        /// </summary>
        /// <param name="providerId">The database ID of the storage provider.</param>
        /// <param name="request">Sync options and parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the sync operation.</returns>
        Task<SyncResult> SyncProviderAsync(long providerId, SyncRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a sync for all enabled storage providers.
        /// </summary>
        /// <param name="request">Sync options and parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of sync results, one per provider.</returns>
        Task<IEnumerable<SyncResult>> SyncAllProvidersAsync(SyncRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current sync status for a provider.
        /// </summary>
        /// <param name="providerId">The database ID of the storage provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current sync status.</returns>
        Task<SyncStatus> GetSyncStatusAsync(long providerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels an in-progress sync operation for a provider.
        /// </summary>
        /// <param name="providerId">The database ID of the storage provider.</param>
        /// <returns>True if a sync was cancelled, false if no sync was in progress.</returns>
        bool CancelSync(long providerId);

        /// <summary>
        /// Scans a storage provider for new files without importing them.
        /// </summary>
        /// <param name="providerId">The database ID of the storage provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Information about files that would be imported.</returns>
        Task<ScanResult> ScanProviderAsync(long providerId, CancellationToken cancellationToken = default);
    }
}
