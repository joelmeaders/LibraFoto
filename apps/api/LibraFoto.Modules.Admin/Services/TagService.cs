using LibraFoto.Modules.Admin.Services.Repositories;
using LibraFoto.Modules.Admin.Services.Shared;

namespace LibraFoto.Modules.Admin.Services;

/// <summary>
/// Implementation of tag management operations.
/// </summary>
public class TagService : ITagService
{
    private readonly ITagRepository _repository;

    public TagService(ITagRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default)
    {
        return await _repository.GetTagsAsync(ct);
    }

    public async Task<TagDto?> GetTagByIdAsync(long id, CancellationToken ct = default)
    {
        return await _repository.GetTagByIdAsync(id, ct);
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        return await _repository.CreateTagAsync(request, ct);
    }

    public async Task<TagDto?> UpdateTagAsync(long id, UpdateTagRequest request, CancellationToken ct = default)
    {
        return await _repository.UpdateTagAsync(id, request, ct);
    }

    public async Task<bool> DeleteTagAsync(long id, CancellationToken ct = default)
    {
        return await _repository.DeleteTagAsync(id, ct);
    }

    public async Task<BulkOperationResult> AddPhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.AddPhotosAsync(tagId, photoIds, ct);
    }

    public async Task<BulkOperationResult> RemovePhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default)
    {
        return await _repository.RemovePhotosAsync(tagId, photoIds, ct);
    }
}
