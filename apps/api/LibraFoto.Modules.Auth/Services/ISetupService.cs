using LibraFoto.Modules.Auth.Models;

namespace LibraFoto.Modules.Auth.Services
{
    /// <summary>
    /// Service interface for initial setup operations.
    /// </summary>
    public interface ISetupService
    {
        /// <summary>
        /// Checks if the initial setup is required (no users exist).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if setup is required, false otherwise.</returns>
        Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes the initial setup by creating the first admin user.
        /// </summary>
        /// <param name="request">Setup request with admin user details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login response for the created admin user, or null if setup is not required.</returns>
        Task<LoginResponse?> CompleteSetupAsync(SetupRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current setup status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Setup status response.</returns>
        Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken = default);
    }
}
