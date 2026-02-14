using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Display.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Display.Services.Repositories;

/// <summary>
/// Entity Framework implementation for slideshow photo queries.
/// </summary>
public sealed class SlideshowRepository : ISlideshowRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public SlideshowRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<long>> GetFilteredPhotoIdsAsync(DisplaySettingsDto settings, CancellationToken cancellationToken = default)
    {
        var query = BuildPhotoQuery(settings);
        return query.Select(p => p.Id).ToListAsync(cancellationToken);
    }

    public async Task<PhotoDto?> GetPhotoDtoByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var photo = await _dbContext.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (photo == null)
        {
            return null;
        }

        return MapToDto(photo);
    }

    public async Task<int> GetPhotoCountAsync(DisplaySettingsDto settings, CancellationToken cancellationToken = default)
    {
        var query = BuildPhotoQuery(settings);
        return await query.CountAsync(cancellationToken);
    }

    private IQueryable<Photo> BuildPhotoQuery(DisplaySettingsDto settings)
    {
        IQueryable<Photo> query = _dbContext.Photos.AsNoTracking();

        switch (settings.SourceType)
        {
            case SourceType.Album when settings.SourceId.HasValue:
                query = query.Where(p => p.PhotoAlbums.Any(pa => pa.AlbumId == settings.SourceId.Value));
                break;

            case SourceType.Tag when settings.SourceId.HasValue:
                query = query.Where(p => p.PhotoTags.Any(pt => pt.TagId == settings.SourceId.Value));
                break;

            case SourceType.All:
            default:
                break;
        }

        return query;
    }

    private static PhotoDto MapToDto(Photo photo)
    {
        return new PhotoDto
        {
            Id = photo.Id,
            Url = $"/api/media/photos/{photo.Id}",
            ThumbnailUrl = photo.ThumbnailPath != null ? $"/api/media/photos/{photo.Id}/thumbnail" : null,
            DateTaken = photo.DateTaken,
            Location = photo.Location,
            MediaType = photo.MediaType,
            Duration = photo.Duration,
            Width = photo.Width,
            Height = photo.Height
        };
    }
}
