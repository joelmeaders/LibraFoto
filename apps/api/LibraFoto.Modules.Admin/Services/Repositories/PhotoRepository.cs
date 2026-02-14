using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Services.Shared;
using LibraFoto.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibraFoto.Modules.Admin.Services.Repositories;

/// <summary>
/// Entity Framework implementation of admin photo data access.
/// </summary>
public sealed class PhotoRepository : IPhotoRepository
{
    private readonly LibraFotoDbContext _db;

    public PhotoRepository(LibraFotoDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<PhotoListDto>> GetPhotosAsync(PhotoFilterRequest filter, CancellationToken ct = default)
    {
        var query = _db.Photos.AsQueryable();

        if (filter.AlbumId.HasValue)
        {
            query = query.Where(p => p.PhotoAlbums.Any(pa => pa.AlbumId == filter.AlbumId.Value));
        }

        if (filter.TagId.HasValue)
        {
            query = query.Where(p => p.PhotoTags.Any(pt => pt.TagId == filter.TagId.Value));
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(p => p.DateTaken >= filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(p => p.DateTaken <= filter.DateTo.Value);
        }

        if (filter.MediaType.HasValue)
        {
            query = query.Where(p => p.MediaType == filter.MediaType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(p => p.Filename.ToLower().Contains(search) ||
                                     (p.Location != null && p.Location.ToLower().Contains(search)));
        }

        var totalItems = await query.CountAsync(ct);

        query = filter.SortBy?.ToLower() switch
        {
            "datetaken" => filter.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(p => p.DateTaken)
                : query.OrderByDescending(p => p.DateTaken),
            "filename" => filter.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(p => p.Filename)
                : query.OrderByDescending(p => p.Filename),
            _ => filter.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(p => p.DateAdded)
                : query.OrderByDescending(p => p.DateAdded)
        };

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var photos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PhotoListDto(
                p.Id,
                p.Filename,
                p.ThumbnailPath ?? p.FilePath,
                p.Width,
                p.Height,
                p.MediaType,
                p.DateTaken,
                p.DateAdded,
                p.Location,
                p.PhotoAlbums.Count,
                p.PhotoTags.Count
            ))
            .ToArrayAsync(ct);

        return new PagedResult<PhotoListDto>(
            photos,
            new PaginationInfo(page, pageSize, totalItems, totalPages)
        );
    }

    public async Task<PhotoDetailDto?> GetPhotoByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Photos
            .Where(p => p.Id == id)
            .Select(p => new PhotoDetailDto(
                p.Id,
                p.Filename,
                p.OriginalFilename,
                p.FilePath,
                p.ThumbnailPath,
                p.Width,
                p.Height,
                p.FileSize,
                p.MediaType,
                p.Duration,
                p.DateTaken,
                p.DateAdded,
                p.Location,
                p.Latitude,
                p.Longitude,
                p.ProviderId,
                p.Provider != null ? p.Provider.Name : null,
                p.PhotoAlbums.Select(pa => new AlbumSummaryDto(pa.Album.Id, pa.Album.Name)).ToArray(),
                p.PhotoTags.Select(pt => new TagSummaryDto(pt.Tag.Id, pt.Tag.Name, pt.Tag.Color)).ToArray()
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PhotoDetailDto?> UpdatePhotoAsync(long id, UpdatePhotoRequest request, CancellationToken ct = default)
    {
        var photo = await _db.Photos.FindAsync([id], ct);
        if (photo is null)
        {
            return null;
        }

        if (request.Filename is not null)
        {
            photo.Filename = request.Filename;
        }

        if (request.Location is not null)
        {
            photo.Location = request.Location;
        }

        if (request.DateTaken.HasValue)
        {
            photo.DateTaken = request.DateTaken.Value;
        }

        await _db.SaveChangesAsync(ct);

        return await GetPhotoByIdAsync(id, ct);
    }

    public async Task<PhotoDeletionData?> GetPhotoDeletionDataAsync(long id, CancellationToken ct = default)
    {
        return await _db.Photos
            .Where(p => p.Id == id)
            .Select(p => new PhotoDeletionData(p.Id, p.FilePath, p.ThumbnailPath, p.ProviderId, p.ProviderFileId))
            .FirstOrDefaultAsync(ct);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        return _db.Database.BeginTransactionAsync(ct);
    }

    public async Task<bool> DeletePhotoRecordAsync(long id, CancellationToken ct = default)
    {
        var photo = await _db.Photos.FindAsync([id], ct);
        if (photo is null)
        {
            return false;
        }

        _db.Photos.Remove(photo);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BulkOperationResult> AddPhotosToAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var successCount = 0;

        var album = await _db.Albums.FindAsync([albumId], ct);
        if (album is null)
        {
            return new BulkOperationResult(0, photoIds.Length, [$"Album {albumId} not found"]);
        }

        var existingPhotoIds = await _db.PhotoAlbums
            .Where(pa => pa.AlbumId == albumId && photoIds.Contains(pa.PhotoId))
            .Select(pa => pa.PhotoId)
            .ToListAsync(ct);

        var maxSortOrder = await _db.PhotoAlbums
            .Where(pa => pa.AlbumId == albumId)
            .MaxAsync(pa => (int?)pa.SortOrder, ct) ?? 0;

        var validPhotoIds = await _db.Photos
            .Where(p => photoIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var notFoundIds = photoIds.Except(validPhotoIds);
        foreach (var id in notFoundIds)
        {
            errors.Add($"Photo {id} not found");
        }

        foreach (var photoId in validPhotoIds)
        {
            if (existingPhotoIds.Contains(photoId))
            {
                errors.Add($"Photo {photoId} already in album");
                continue;
            }

            _db.PhotoAlbums.Add(new PhotoAlbum
            {
                PhotoId = photoId,
                AlbumId = albumId,
                SortOrder = ++maxSortOrder,
                DateAdded = DateTime.UtcNow
            });
            successCount++;
        }

        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(successCount, errors.Count, errors.ToArray());
    }

    public async Task<BulkOperationResult> RemovePhotosFromAlbumAsync(long albumId, long[] photoIds, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var album = await _db.Albums.FindAsync([albumId], ct);
        if (album is null)
        {
            return new BulkOperationResult(0, photoIds.Length, [$"Album {albumId} not found"]);
        }

        var photoAlbums = await _db.PhotoAlbums
            .Where(pa => pa.AlbumId == albumId && photoIds.Contains(pa.PhotoId))
            .ToListAsync(ct);

        var foundIds = photoAlbums.Select(pa => pa.PhotoId).ToHashSet();
        var notFoundIds = photoIds.Where(id => !foundIds.Contains(id));
        foreach (var id in notFoundIds)
        {
            errors.Add($"Photo {id} not in album");
        }

        _db.PhotoAlbums.RemoveRange(photoAlbums);
        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(photoAlbums.Count, errors.Count, errors.ToArray());
    }

    public async Task<BulkOperationResult> AddTagsToPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var successCount = 0;

        var validPhotoIds = await _db.Photos
            .Where(p => photoIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var validTagIds = await _db.Tags
            .Where(t => tagIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        var notFoundPhotos = photoIds.Except(validPhotoIds);
        foreach (var id in notFoundPhotos)
        {
            errors.Add($"Photo {id} not found");
        }

        var notFoundTags = tagIds.Except(validTagIds);
        foreach (var id in notFoundTags)
        {
            errors.Add($"Tag {id} not found");
        }

        var existingPairs = await _db.PhotoTags
            .Where(pt => photoIds.Contains(pt.PhotoId) && tagIds.Contains(pt.TagId))
            .Select(pt => new { pt.PhotoId, pt.TagId })
            .ToListAsync(ct);

        var existingSet = existingPairs.Select(p => (p.PhotoId, p.TagId)).ToHashSet();

        foreach (var photoId in validPhotoIds)
        {
            foreach (var tagId in validTagIds)
            {
                if (existingSet.Contains((photoId, tagId)))
                {
                    continue;
                }

                _db.PhotoTags.Add(new PhotoTag
                {
                    PhotoId = photoId,
                    TagId = tagId,
                    DateAdded = DateTime.UtcNow
                });
                successCount++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(successCount, errors.Count, errors.ToArray());
    }

    public async Task<BulkOperationResult> RemoveTagsFromPhotosAsync(long[] photoIds, long[] tagIds, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var photoTags = await _db.PhotoTags
            .Where(pt => photoIds.Contains(pt.PhotoId) && tagIds.Contains(pt.TagId))
            .ToListAsync(ct);

        _db.PhotoTags.RemoveRange(photoTags);
        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(photoTags.Count, errors.Count, errors.ToArray());
    }

    public async Task<PhotoCountDto> GetPhotoCountAsync(CancellationToken ct = default)
    {
        var count = await _db.Photos.CountAsync(ct);
        return new PhotoCountDto(count);
    }
}
