namespace LibraFoto.Modules.Admin.Models
{
    /// <summary>
    /// Request for bulk photo operations.
    /// </summary>
    public record BulkPhotoRequest(
        long[] PhotoIds
    );

    /// <summary>
    /// Request to add photos to an album.
    /// </summary>
    public record AddPhotosToAlbumRequest(
        long[] PhotoIds
    );

    /// <summary>
    /// Request to remove photos from an album.
    /// </summary>
    public record RemovePhotosFromAlbumRequest(
        long[] PhotoIds
    );

    /// <summary>
    /// Request to add tags to photos.
    /// </summary>
    public record AddTagsToPhotosRequest(
        long[] PhotoIds,
        long[] TagIds
    );

    /// <summary>
    /// Request to remove tags from photos.
    /// </summary>
    public record RemoveTagsFromPhotosRequest(
        long[] PhotoIds,
        long[] TagIds
    );

    /// <summary>
    /// Result of a bulk operation.
    /// </summary>
    public record BulkOperationResult(
        int SuccessCount,
        int FailedCount,
        string[] Errors
    );
}
