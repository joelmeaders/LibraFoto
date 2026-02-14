using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Display.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Display.Services.Repositories;

/// <summary>
/// Entity Framework implementation of display settings repository.
/// </summary>
public sealed class DisplaySettingsRepository : IDisplaySettingsRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public DisplaySettingsRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DisplaySettingsDto?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        return settings != null ? MapToDto(settings) : null;
    }

    public async Task<DisplaySettingsDto?> GetFirstAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return settings != null ? MapToDto(settings) : null;
    }

    public async Task<DisplaySettingsDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return settings != null ? MapToDto(settings) : null;
    }

    public async Task<IReadOnlyList<DisplaySettingsDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return settings.Select(MapToDto).ToList();
    }

    public async Task<DisplaySettingsDto?> UpdateAsync(long id, UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return null;
        }

        ApplyUpdates(settings, request);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(settings);
    }

    public async Task<DisplaySettingsDto> CreateAsync(UpdateDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = new DisplaySettings
        {
            Name = request.Name ?? "New Configuration"
        };

        ApplyUpdates(settings, request);

        _dbContext.DisplaySettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(settings);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return false;
        }

        var count = await _dbContext.DisplaySettings.CountAsync(cancellationToken);
        if (count <= 1)
        {
            return false;
        }

        _dbContext.DisplaySettings.Remove(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

    public async Task<DisplaySettingsDto?> SetActiveAsync(long id, CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.DisplaySettings
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settings == null)
        {
            return null;
        }

        var allSettings = await _dbContext.DisplaySettings.ToListAsync(cancellationToken);
        foreach (var s in allSettings)
        {
            s.IsActive = s.Id == id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(settings);
    }

    public async Task<DisplaySettingsDto> CreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        var settings = new DisplaySettings
        {
            Name = "Default",
            IsActive = true
        };

        _dbContext.DisplaySettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(settings);
    }

    private static void ApplyUpdates(DisplaySettings settings, UpdateDisplaySettingsRequest request)
    {
        if (request.Name != null)
        {
            settings.Name = request.Name;
        }

        if (request.SlideDuration.HasValue)
        {
            settings.SlideDuration = request.SlideDuration.Value;
        }

        if (request.Transition.HasValue)
        {
            settings.Transition = request.Transition.Value;
        }

        if (request.TransitionDuration.HasValue)
        {
            settings.TransitionDuration = request.TransitionDuration.Value;
        }

        if (request.SourceType.HasValue)
        {
            settings.SourceType = request.SourceType.Value;
        }

        if (request.SourceId.HasValue)
        {
            settings.SourceId = request.SourceId.Value;
        }

        if (request.Shuffle.HasValue)
        {
            settings.Shuffle = request.Shuffle.Value;
        }

        if (request.ImageFit.HasValue)
        {
            settings.ImageFit = request.ImageFit.Value;
        }
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
