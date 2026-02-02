using LibraFoto.Data.Entities;

namespace LibraFoto.Modules.Storage.Interfaces;

/// <summary>
/// Interface for storage providers that use OAuth authentication.
/// Provides methods for disconnecting and clearing OAuth tokens.
/// </summary>
public interface IOAuthProvider
{
    /// <summary>
    /// Disconnects the provider by clearing OAuth tokens from the configuration
    /// and disabling the provider. The caller is responsible for saving changes to the database.
    /// </summary>
    /// <param name="providerEntity">The storage provider entity to disconnect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if disconnect was successful, false otherwise.</returns>
    Task<bool> DisconnectAsync(StorageProvider providerEntity, CancellationToken cancellationToken = default);
}
