using LibraFoto.Data;
using LibraFoto.Data.Entities;

namespace LibraFoto.Modules.Media.Services.Repositories;

/// <summary>
/// Entity Framework implementation of media photo repository.
/// </summary>
public sealed class MediaPhotoRepository : IMediaPhotoRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public MediaPhotoRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Photo?> GetPhotoByIdAsync(long photoId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Photos.FindAsync([photoId], cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
