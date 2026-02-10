using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Interfaces;

/// <summary>
/// Factory for creating and managing storage provider instances.
/// </summary>
public interface IStorageProviderFactory
{
    /// <summary>
    /// Gets a storage provider by its database ID.
    /// </summary>
    /// <param name="providerId">The database ID of the storage provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The storage provider instance, or null if not found.</returns>
    Task<IStorageProvider?> GetProviderAsync(long providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configured and enabled storage providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of storage provider instances.</returns>
    Task<IEnumerable<IStorageProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all storage providers of a specific type.
    /// </summary>
    /// <param name="type">The type of storage provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of storage provider instances.</returns>
    Task<IEnumerable<IStorageProvider>> GetProvidersByTypeAsync(StorageProviderType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new storage provider instance for a specific type.
    /// This does not persist to database; use for configuration testing.
    /// </summary>
    /// <param name="type">The type of storage provider to create.</param>
    /// <returns>New storage provider instance.</returns>
    IStorageProvider CreateProvider(StorageProviderType type);

    /// <summary>
    /// Gets the default local storage provider, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default local storage provider.</returns>
    Task<IStorageProvider> GetOrCreateDefaultLocalProviderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears any cached provider instances, forcing reload from database.
    /// </summary>
    void ClearCache();
}
