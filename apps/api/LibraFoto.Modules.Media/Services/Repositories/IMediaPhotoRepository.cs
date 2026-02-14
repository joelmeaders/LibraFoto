using LibraFoto.Data.Entities;

namespace LibraFoto.Modules.Media.Services.Repositories;

/// <summary>
/// Repository abstraction for media photo read/write operations used by endpoints.
/// </summary>
public interface IMediaPhotoRepository
{
    Task<Photo?> GetPhotoByIdAsync(long photoId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
