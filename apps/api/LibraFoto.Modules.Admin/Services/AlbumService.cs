using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Admin.Services
{
    /// <summary>
    /// Implementation of album management operations.
    /// </summary>
    public class AlbumService : IAlbumService
    {
        private readonly LibraFotoDbContext _db;

        public AlbumService(LibraFotoDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AlbumDto>> GetAlbumsAsync(CancellationToken ct = default)
        {
            return await _db.Albums
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .Select(a => new AlbumDto(
                    a.Id,
                    a.Name,
                    a.Description,
                    a.CoverPhotoId,
                    a.CoverPhoto != null ? a.CoverPhoto.ThumbnailPath ?? a.CoverPhoto.FilePath : null,
                    a.DateCreated,
                    a.SortOrder,
                    a.PhotoAlbums.Count
                ))
                .ToListAsync(ct);
        }

        public async Task<AlbumDto?> GetAlbumByIdAsync(long id, CancellationToken ct = default)
        {
            return await _db.Albums
                .Where(a => a.Id == id)
                .Select(a => new AlbumDto(
                    a.Id,
                    a.Name,
                    a.Description,
                    a.CoverPhotoId,
                    a.CoverPhoto != null ? a.CoverPhoto.ThumbnailPath ?? a.CoverPhoto.FilePath : null,
                    a.DateCreated,
                    a.SortOrder,
                    a.PhotoAlbums.Count
                ))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default)
        {
            var maxSortOrder = await _db.Albums.MaxAsync(a => (int?)a.SortOrder, ct) ?? 0;

            var album = new Album
            {
                Name = request.Name,
                Description = request.Description,
                CoverPhotoId = request.CoverPhotoId,
                DateCreated = DateTime.UtcNow,
                SortOrder = maxSortOrder + 1
            };

            _db.Albums.Add(album);
            await _db.SaveChangesAsync(ct);

            return (await GetAlbumByIdAsync(album.Id, ct))!;
        }

        public async Task<AlbumDto?> UpdateAlbumAsync(long id, UpdateAlbumRequest request, CancellationToken ct = default)
        {
            var album = await _db.Albums.FindAsync([id], ct);
            if (album is null)
            {
                return null;
            }

            if (request.Name is not null)
            {
                album.Name = request.Name;
            }

            if (request.Description is not null)
            {
                album.Description = request.Description;
            }

            if (request.CoverPhotoId.HasValue)
            {
                album.CoverPhotoId = request.CoverPhotoId.Value;
            }

            if (request.SortOrder.HasValue)
            {
                album.SortOrder = request.SortOrder.Value;
            }

            await _db.SaveChangesAsync(ct);

            return await GetAlbumByIdAsync(id, ct);
        }

        public async Task<bool> DeleteAlbumAsync(long id, CancellationToken ct = default)
        {
            var album = await _db.Albums.FindAsync([id], ct);
            if (album is null)
            {
                return false;
            }

            _db.Albums.Remove(album);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<AlbumDto?> SetCoverPhotoAsync(long albumId, long photoId, CancellationToken ct = default)
        {
            var album = await _db.Albums.FindAsync([albumId], ct);
            if (album is null)
            {
                return null;
            }

            var photo = await _db.Photos.FindAsync([photoId], ct);
            if (photo is null)
            {
                return null;
            }

            album.CoverPhotoId = photoId;
            await _db.SaveChangesAsync(ct);

            return await GetAlbumByIdAsync(albumId, ct);
        }

        public async Task<AlbumDto?> RemoveCoverPhotoAsync(long albumId, CancellationToken ct = default)
        {
            var album = await _db.Albums.FindAsync([albumId], ct);
            if (album is null)
            {
                return null;
            }

            album.CoverPhotoId = null;
            await _db.SaveChangesAsync(ct);

            return await GetAlbumByIdAsync(albumId, ct);
        }

        public async Task<BulkOperationResult> AddPhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default)
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

        public async Task<BulkOperationResult> RemovePhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default)
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

        public async Task<bool> ReorderPhotosAsync(long albumId, PhotoOrder[] orders, CancellationToken ct = default)
        {
            var album = await _db.Albums.FindAsync([albumId], ct);
            if (album is null)
            {
                return false;
            }

            var photoIds = orders.Select(o => o.PhotoId).ToList();
            var photoAlbums = await _db.PhotoAlbums
                .Where(pa => pa.AlbumId == albumId && photoIds.Contains(pa.PhotoId))
                .ToListAsync(ct);

            var orderLookup = orders.ToDictionary(o => o.PhotoId, o => o.SortOrder);

            foreach (var pa in photoAlbums)
            {
                if (orderLookup.TryGetValue(pa.PhotoId, out var sortOrder))
                {
                    pa.SortOrder = sortOrder;
                }
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
