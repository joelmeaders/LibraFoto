using LibraFoto.Modules.Display.Services.Shared;

namespace LibraFoto.Modules.Display.Services.Repositories;

/// <summary>
/// Repository abstraction for slideshow photo retrieval queries.
/// </summary>
public interface ISlideshowRepository
{
    Task<List<long>> GetFilteredPhotoIdsAsync(DisplaySettingsDto settings, CancellationToken cancellationToken = default);
    Task<PhotoDto?> GetPhotoDtoByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<int> GetPhotoCountAsync(DisplaySettingsDto settings, CancellationToken cancellationToken = default);
}
