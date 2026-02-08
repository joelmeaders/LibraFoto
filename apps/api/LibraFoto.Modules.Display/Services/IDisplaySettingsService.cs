using LibraFoto.Modules.Display.Models;

namespace LibraFoto.Modules.Display.Services
{
    /// <summary>
    /// Interface for display settings operations.
    /// Manages CRUD operations for display configurations.
    /// </summary>
    public interface IDisplaySettingsService
    {
        /// <summary>
        /// Gets the currently active display settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The active display settings, or default settings if none configured.</returns>
        Task<DisplaySettingsDto> GetActiveSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets display settings by ID.
        /// </summary>
        /// <param name="id">Settings ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The settings if found, or null.</returns>
        Task<DisplaySettingsDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all display settings configurations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all display settings.</returns>
        Task<IReadOnlyList<DisplaySettingsDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates display settings.
        /// </summary>
        /// <param name="id">Settings ID to update.</param>
        /// <param name="request">Update request with new values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated settings, or null if not found.</returns>
        Task<DisplaySettingsDto?> UpdateAsync(long id, UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates new display settings.
        /// </summary>
        /// <param name="request">Settings to create.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created settings.</returns>
        Task<DisplaySettingsDto> CreateAsync(UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes display settings.
        /// </summary>
        /// <param name="id">Settings ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted, false if not found.</returns>
        Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a display settings configuration as the active one.
        /// </summary>
        /// <param name="id">Settings ID to activate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The activated settings, or null if not found.</returns>
        Task<DisplaySettingsDto?> SetActiveAsync(long id, CancellationToken cancellationToken = default);
    }
}
