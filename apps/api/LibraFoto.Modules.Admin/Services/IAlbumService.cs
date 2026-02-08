using LibraFoto.Modules.Admin.Models;

namespace LibraFoto.Modules.Admin.Services
{
    /// <summary>
    /// Service interface for album management operations.
    /// </summary>
    public interface IAlbumService
    {
        /// <summary>
        /// Gets all albums.
        /// </summary>
        Task<IReadOnlyList<AlbumDto>> GetAlbumsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets a single album by ID.
        /// </summary>
        Task<AlbumDto?> GetAlbumByIdAsync(long id, CancellationToken ct = default);

        /// <summary>
        /// Creates a new album.
        /// </summary>
        Task<AlbumDto> CreateAlbumAsync(CreateAlbumRequest request, CancellationToken ct = default);

        /// <summary>
        /// Updates an album.
        /// </summary>
        Task<AlbumDto?> UpdateAlbumAsync(long id, UpdateAlbumRequest request, CancellationToken ct = default);

        /// <summary>
        /// Deletes an album.
        /// </summary>
        Task<bool> DeleteAlbumAsync(long id, CancellationToken ct = default);

        /// <summary>
        /// Sets the cover photo for an album.
        /// </summary>
        Task<AlbumDto?> SetCoverPhotoAsync(long albumId, long photoId, CancellationToken ct = default);

        /// <summary>
        /// Removes the cover photo from an album.
        /// </summary>
        Task<AlbumDto?> RemoveCoverPhotoAsync(long albumId, CancellationToken ct = default);

        /// <summary>
        /// Adds photos to an album.
        /// </summary>
        Task<BulkOperationResult> AddPhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default);

        /// <summary>
        /// Removes photos from an album.
        /// </summary>
        Task<BulkOperationResult> RemovePhotosAsync(long albumId, long[] photoIds, CancellationToken ct = default);

        /// <summary>
        /// Reorders photos in an album.
        /// </summary>
        Task<bool> ReorderPhotosAsync(long albumId, PhotoOrder[] orders, CancellationToken ct = default);
    }
}
