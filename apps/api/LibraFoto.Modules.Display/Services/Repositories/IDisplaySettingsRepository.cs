using LibraFoto.Modules.Display.Services.Shared;

namespace LibraFoto.Modules.Display.Services.Repositories;

/// <summary>
/// Repository abstraction for display settings persistence.
/// </summary>
public interface IDisplaySettingsRepository
{
    Task<DisplaySettingsDto?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto?> GetFirstAsync(CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DisplaySettingsDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto?> UpdateAsync(long id, UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto> CreateAsync(UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto?> SetActiveAsync(long id, CancellationToken cancellationToken = default);
    Task<DisplaySettingsDto> CreateDefaultAsync(CancellationToken cancellationToken = default);
}
