using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Display.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Display.Services;

/// <summary>
/// Service for managing display settings.
/// Provides CRUD operations for display configurations.
/// </summary>
public class DisplaySettingsService : IDisplaySettingsService
{
    private readonly LibraFotoDbContext _dbContext;
    private readonly ILogger<DisplaySettingsService> _logger;

    public DisplaySettingsService(
        LibraFotoDbContext dbContext,
        ILogger<DisplaySettingsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto> GetActiveSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        if (settings != null)
        {
            return MapToDto(settings);
        }

        // No active settings, try to get the first one
        settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings != null)
        {
            return MapToDto(settings);
        }

        // No settings exist, create default
        _logger.LogInformation("No display settings found, creating default settings");
        return await CreateDefaultSettingsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return settings != null ? MapToDto(settings) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisplaySettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return settings.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> UpdateAsync(long id, UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return null;
        }

        // Apply updates
        ApplyUpdates(settings, request);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated display settings {SettingsId}", id);

        return MapToDto(settings);
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto> CreateAsync(UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = new DisplaySettings
        {
            Name = request.Name ?? "New Configuration"
        };

        ApplyUpdates(settings, request);

        _dbContext.DisplaySettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created display settings {SettingsId} with name '{Name}'", settings.Id, settings.Name);

        return MapToDto(settings);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return false;
        }

        // Prevent deleting the only settings
        var count = await _dbContext.DisplaySettings.CountAsync(cancellationToken);
        if (count <= 1)
        {
            _logger.LogWarning("Cannot delete the last display settings configuration");
            return false;
        }

        _dbContext.DisplaySettings.Remove(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted display settings {SettingsId}", id);

        // If we deleted the active settings, activate another one
        if (settings.IsActive)
        {
            var newActive = await _dbContext.DisplaySettings.FirstOrDefaultAsync(cancellationToken);
            if (newActive != null)
            {
                newActive.IsActive = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<DisplaySettingsDto?> SetActiveAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return null;
        }

        // Deactivate all other settings
        var allSettings = await _dbContext.DisplaySettings.ToListAsync(cancellationToken);
        foreach (var s in allSettings)
        {
            s.IsActive = s.Id == id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Set display settings {SettingsId} as active", id);

        return MapToDto(settings);
    }

    private async Task<DisplaySettingsDto> CreateDefaultSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = new DisplaySettings
        {
            Name = "Default",
            IsActive = true
        };

        _dbContext.DisplaySettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default display settings with ID {SettingsId}", settings.Id);

        return MapToDto(settings);
    }

    private static void ApplyUpdates(DisplaySettings settings, UpdateDisplaySettingsRequest request)
    {
        if (request.Name != null)
            settings.Name = request.Name;

        if (request.SlideDuration.HasValue)
            settings.SlideDuration = request.SlideDuration.Value;

        if (request.Transition.HasValue)
            settings.Transition = request.Transition.Value;

        if (request.TransitionDuration.HasValue)
            settings.TransitionDuration = request.TransitionDuration.Value;

        if (request.SourceType.HasValue)
            settings.SourceType = request.SourceType.Value;

        if (request.SourceId.HasValue)
            settings.SourceId = request.SourceId.Value;

        if (request.Shuffle.HasValue)
            settings.Shuffle = request.Shuffle.Value;

        if (request.ImageFit.HasValue)
            settings.ImageFit = request.ImageFit.Value;
    }

    private static DisplaySettingsDto MapToDto(DisplaySettings settings)
    {
        return new DisplaySettingsDto
        {
            Id = settings.Id,
            Name = settings.Name,
            SlideDuration = settings.SlideDuration,
            Transition = settings.Transition,
            TransitionDuration = settings.TransitionDuration,
            SourceType = settings.SourceType,
            SourceId = settings.SourceId,
            Shuffle = settings.Shuffle,
            ImageFit = settings.ImageFit
        };
    }
}
