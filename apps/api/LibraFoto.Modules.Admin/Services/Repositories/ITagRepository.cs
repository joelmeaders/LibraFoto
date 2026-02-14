using LibraFoto.Modules.Admin.Services.Shared;

namespace LibraFoto.Modules.Admin.Services.Repositories;

/// <summary>
/// Data-access operations for tags.
/// </summary>
public interface ITagRepository
{
    Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default);
    Task<TagDto?> GetTagByIdAsync(long id, CancellationToken ct = default);
    Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<TagDto?> UpdateTagAsync(long id, UpdateTagRequest request, CancellationToken ct = default);
    Task<bool> DeleteTagAsync(long id, CancellationToken ct = default);
    Task<BulkOperationResult> AddPhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default);
    Task<BulkOperationResult> RemovePhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default);
}
