using LibraFoto.Modules.Admin.Models;

namespace LibraFoto.Modules.Admin.Services
{
    /// <summary>
    /// Service interface for tag management operations.
    /// </summary>
    public interface ITagService
    {
        /// <summary>
        /// Gets all tags.
        /// </summary>
        Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets a single tag by ID.
        /// </summary>
        Task<TagDto?> GetTagByIdAsync(long id, CancellationToken ct = default);

        /// <summary>
        /// Creates a new tag.
        /// </summary>
        Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken ct = default);

        /// <summary>
        /// Updates a tag.
        /// </summary>
        Task<TagDto?> UpdateTagAsync(long id, UpdateTagRequest request, CancellationToken ct = default);

        /// <summary>
        /// Deletes a tag.
        /// </summary>
        Task<bool> DeleteTagAsync(long id, CancellationToken ct = default);

        /// <summary>
        /// Adds photos to a tag.
        /// </summary>
        Task<BulkOperationResult> AddPhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default);

        /// <summary>
        /// Removes photos from a tag.
        /// </summary>
        Task<BulkOperationResult> RemovePhotosAsync(long tagId, long[] photoIds, CancellationToken ct = default);
    }
}
