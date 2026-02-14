using LibraFoto.Modules.Display.Services.Repositories;
using LibraFoto.Modules.Display.Services.Shared;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Display.Services;

/// <summary>
/// Service for managing display settings.
/// Provides CRUD operations for display configurations.
/// </summary>
public class DisplaySettingsService : IDisplaySettingsService
{
    private readonly IDisplaySettingsRepository _settingsRepository;
    private readonly ILogger<DisplaySettingsService> _logger;

    public DisplaySettingsService(
        IDisplaySettingsRepository settingsRepository,
        ILogger<DisplaySettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto> GetActiveSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetActiveAsync(cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        settings = await _settingsRepository.GetFirstAsync(cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        // No settings exist, create default
        _logger.LogInformation("No display settings found, creating default settings");
        return await _settingsRepository.CreateDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _settingsRepository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisplaySettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsRepository.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> UpdateAsync(long id, UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _settingsRepository.UpdateAsync(id, request, cancellationToken);
        if (result != null)
        {
            _logger.LogInformation("Updated display settings {SettingsId}", id);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto> CreateAsync(UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _settingsRepository.CreateAsync(request, cancellationToken);
        _logger.LogInformation("Created display settings {SettingsId} with name '{Name}'", result.Id, result.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var deleted = await _settingsRepository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            _logger.LogWarning("Cannot delete display settings {SettingsId}", id);
            return false;
        }

        _logger.LogInformation("Deleted display settings {SettingsId}", id);
        return true;
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> SetActiveAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await _settingsRepository.SetActiveAsync(id, cancellationToken);
        if (result != null)
        {
            _logger.LogInformation("Set display settings {SettingsId} as active", id);
        }

        return result;
    }
}
