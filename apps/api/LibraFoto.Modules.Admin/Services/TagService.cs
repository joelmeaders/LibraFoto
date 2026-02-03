using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Admin.Services;

/// <summary>
/// Implementation of tag management operations.
/// </summary>
public class TagService : ITagService
{
    private readonly LibraFotoDbContext _db;

    public TagService(LibraFotoDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default)
    {
        return await _db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(
                t.Id,
                t.Name,
                t.Color,
                t.PhotoTags.Count
            ))
            .ToListAsync(ct);
    }

    public async Task<TagDto?> GetTagByIdAsync(long id, CancellationToken ct = default)
    {
        return await _db.Tags
            .Where(t => t.Id == id)
            .Select(t => new TagDto(
                t.Id,
                t.Name,
                t.Color,
                t.PhotoTags.Count
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        var tag = new Tag
        {
            Name = request.Name,
            Color = request.Color
        };

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(ct);

        return new TagDto(tag.Id, tag.Name, tag.Color, 0);
    }

    public async Task<TagDto?> UpdateTagAsync(long id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tag = await _db.Tags.FindAsync([id], ct);
        if (tag is null)
        {
            return null;
        }

        if (request.Name is not null)
        {
            tag.Name = request.Name;
        }

        if (request.Color is not null)
        {
            tag.Color = request.Color;
        }

        await _db.SaveChangesAsync(ct);

        return await GetTagByIdAsync(id, ct);
    }

    public async Task<bool> DeleteTagAsync(long id, CancellationToken ct = default)
    {
        var tag = await _db.Tags.FindAsync([id], ct);
        if (tag is null)
        {
            return false;
        }

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BulkOperationResult> AddPhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var successCount = 0;

        var tag = await _db.Tags.FindAsync([tagId], ct);
        if (tag is null)
        {
            return new BulkOperationResult(0, photoIds.Length, [$"Tag {tagId} not found"]);
        }

        var existingPhotoIds = await _db.PhotoTags
            .Where(pt => pt.TagId == tagId && photoIds.Contains(pt.PhotoId))
            .Select(pt => pt.PhotoId)
            .ToListAsync(ct);

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
                errors.Add($"Photo {photoId} already has tag");
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

        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(successCount, errors.Count, errors.ToArray());
    }

    public async Task<BulkOperationResult> RemovePhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default)
    {
        var errors = new List<string>();

        var tag = await _db.Tags.FindAsync([tagId], ct);
        if (tag is null)
        {
            return new BulkOperationResult(0, photoIds.Length, [$"Tag {tagId} not found"]);
        }

        var photoTags = await _db.PhotoTags
            .Where(pt => pt.TagId == tagId && photoIds.Contains(pt.PhotoId))
            .ToListAsync(ct);

        var foundIds = photoTags.Select(pt => pt.PhotoId).ToHashSet();
        var notFoundIds = photoIds.Where(id => !foundIds.Contains(id));
        foreach (var id in notFoundIds)
        {
            errors.Add($"Photo {id} does not have tag");
        }

        _db.PhotoTags.RemoveRange(photoTags);
        await _db.SaveChangesAsync(ct);

        return new BulkOperationResult(photoTags.Count, errors.Count, errors.ToArray());
    }
}
