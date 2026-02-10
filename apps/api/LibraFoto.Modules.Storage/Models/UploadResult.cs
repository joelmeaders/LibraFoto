namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Result of an upload operation.
/// </summary>
public record UploadResult
{
    /// <summary>
    /// Whether the upload was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Database ID of the created photo record.
    /// </summary>
    public long? PhotoId { get; init; }

    /// <summary>
    /// File ID within the storage provider.
    /// </summary>
    public string? FileId { get; init; }

    /// <summary>
    /// Final filename after any renaming.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Relative path where the file was stored.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// MIME content type.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// URL to access the uploaded file (for immediate preview).
    /// </summary>
    public string? FileUrl { get; init; }

    /// <summary>
    /// URL to the thumbnail (once generated).
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Creates a successful upload result.
    /// </summary>
    public static UploadResult Successful(long photoId, string fileId, string fileName, string filePath, long fileSize, string contentType) =>
        new()
        {
            Success = true,
            PhotoId = photoId,
            FileId = fileId,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = contentType
        };

    /// <summary>
    /// Creates a failed upload result.
    /// </summary>
    public static UploadResult Failed(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Result of a batch upload operation.
/// </summary>
public record BatchUploadResult
{
    /// <summary>
    /// Total number of files in the batch.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Number of successfully uploaded files.
    /// </summary>
    public int SuccessfulUploads { get; init; }

    /// <summary>
    /// Number of failed uploads.
    /// </summary>
    public int FailedUploads { get; init; }

    /// <summary>
    /// Individual results for each file.
    /// </summary>
    public List<UploadResult> Results { get; init; } = [];

    /// <summary>
    /// Whether all uploads succeeded.
    /// </summary>
    public bool AllSuccessful => FailedUploads == 0;
}
