using System.Text.Json.Serialization;
using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Services.Shared;

public class GooglePhotosConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? AccessTokenExpiry { get; set; }
    public string[]? GrantedScopes { get; set; }
}

public record ImageImportResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? FilePath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSize { get; init; }
    public bool WasResized { get; init; }
    public int OriginalWidth { get; init; }
    public int OriginalHeight { get; init; }

    public static ImageImportResult Successful(
        string filePath,
        int width,
        int height,
        long fileSize,
        bool wasResized,
        int originalWidth,
        int originalHeight) => new()
        {
            Success = true,
            FilePath = filePath,
            Width = width,
            Height = height,
            FileSize = fileSize,
            WasResized = wasResized,
            OriginalWidth = originalWidth,
            OriginalHeight = originalHeight
        };

    public static ImageImportResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

public record ImageMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime? DateTaken { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Location { get; init; }
}

public record ScannedFile
{
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public long FileSize { get; init; }
    public required string ContentType { get; init; }
    public MediaType MediaType { get; init; }
    public DateTime CreatedTime { get; init; }
    public DateTime ModifiedTime { get; init; }
    public bool IsHidden { get; init; }
}

public record StorageFileInfo
{
    public required string FileId { get; init; }
    public required string FileName { get; init; }
    public string? FullPath { get; init; }
    public long FileSize { get; init; }
    public string? ContentType { get; init; }
    public MediaType MediaType { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public string? ContentHash { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? Duration { get; init; }
    public bool IsFolder { get; init; }
    public string? ParentFolderId { get; init; }
}

public record LocalStorageConfiguration
{
    public string BasePath { get; init; } = "./photos";
    public bool OrganizeByDate { get; init; } = true;
    public bool WatchForChanges { get; init; } = true;
    public int MaxImportDimension { get; init; } = 2560;
}

public record SyncRequest
{
    public bool FullSync { get; init; } = false;
    public bool RemoveDeleted { get; init; } = true;
    public bool SkipExisting { get; init; } = true;
    public int MaxFiles { get; init; } = 0;
    public string? FolderId { get; init; }
    public bool Recursive { get; init; } = true;
}

public record SyncResult
{
    public long ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int FilesAdded { get; init; }
    public int FilesUpdated { get; init; }
    public int FilesRemoved { get; init; }
    public int FilesSkipped { get; init; }
    public int TotalFilesProcessed { get; init; }
    public int TotalFilesFound { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    public List<string> Errors { get; init; } = [];

    public static SyncResult Successful(long providerId, string providerName, int added, int updated, int removed, int skipped, int total, DateTime start) =>
        new()
        {
            ProviderId = providerId,
            ProviderName = providerName,
            Success = true,
            FilesAdded = added,
            FilesUpdated = updated,
            FilesRemoved = removed,
            FilesSkipped = skipped,
            TotalFilesProcessed = added + updated + removed + skipped,
            TotalFilesFound = total,
            StartTime = start,
            EndTime = DateTime.UtcNow
        };

    public static SyncResult Failed(long providerId, string providerName, string errorMessage, DateTime start) =>
        new()
        {
            ProviderId = providerId,
            ProviderName = providerName,
            Success = false,
            ErrorMessage = errorMessage,
            StartTime = start,
            EndTime = DateTime.UtcNow
        };
}

public record SyncStatus
{
    public long ProviderId { get; init; }
    public bool IsInProgress { get; init; }
    public int ProgressPercent { get; init; }
    public string? CurrentOperation { get; init; }
    public int FilesProcessed { get; init; }
    public int? TotalFiles { get; init; }
    public DateTime? StartTime { get; init; }
    public SyncResult? LastSyncResult { get; init; }
}

public record ScanResult
{
    public long ProviderId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalFilesFound { get; init; }
    public int NewFilesCount { get; init; }
    public int ExistingFilesCount { get; init; }
    public long NewFilesTotalSize { get; init; }
    public List<StorageFileInfo> SampleNewFiles { get; init; } = [];
}

public record UploadResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long? PhotoId { get; init; }
    public string? FileId { get; init; }
    public string? FileName { get; init; }
    public string? FilePath { get; init; }
    public long FileSize { get; init; }
    public string? ContentType { get; init; }
    public string? FileUrl { get; init; }
    public string? ThumbnailUrl { get; init; }

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

    public static UploadResult Failed(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

public record BatchUploadResult
{
    public int TotalFiles { get; init; }
    public int SuccessfulUploads { get; init; }
    public int FailedUploads { get; init; }
    public List<UploadResult> Results { get; init; } = [];
    public bool AllSuccessful => FailedUploads == 0;
}

public record PickerSessionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("pickerUri")]
    public string? PickerUri { get; init; }

    [JsonPropertyName("pollingConfig")]
    public PickerPollingConfigResponse? PollingConfig { get; init; }

    [JsonPropertyName("expireTime")]
    public DateTime? ExpireTime { get; init; }

    [JsonPropertyName("mediaItemsSet")]
    public bool MediaItemsSet { get; init; }
}

public record PickerPollingConfigResponse
{
    [JsonPropertyName("pollInterval")]
    public string? PollInterval { get; init; }

    [JsonPropertyName("timeoutIn")]
    public string? TimeoutIn { get; init; }
}

public record PickedMediaItemsResponse
{
    [JsonPropertyName("mediaItems")]
    public List<PickedMediaItemResponse>? MediaItems { get; init; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; init; }
}

public record PickedMediaItemResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("createTime")]
    public DateTime? CreateTime { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("mediaFile")]
    public PickedMediaFileResponse? MediaFile { get; init; }
}

public record PickedMediaFileResponse
{
    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("mediaFileMetadata")]
    public PickedMediaFileMetadataResponse? MediaFileMetadata { get; init; }
}

public record PickedMediaFileMetadataResponse
{
    [JsonPropertyName("width")]
    public int? Width { get; init; }

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("photoMetadata")]
    public PickedPhotoMetadataResponse? PhotoMetadata { get; init; }

    [JsonPropertyName("videoMetadata")]
    public PickedVideoMetadataResponse? VideoMetadata { get; init; }
}

public record PickedPhotoMetadataResponse
{
    [JsonPropertyName("focalLength")]
    public double? FocalLength { get; init; }

    [JsonPropertyName("apertureFNumber")]
    public double? ApertureFNumber { get; init; }

    [JsonPropertyName("isoEquivalent")]
    public int? IsoEquivalent { get; init; }

    [JsonPropertyName("exposureTime")]
    public string? ExposureTime { get; init; }
}

public record PickedVideoMetadataResponse
{
    [JsonPropertyName("fps")]
    public double? Fps { get; init; }

    [JsonPropertyName("processingStatus")]
    public string? ProcessingStatus { get; init; }
}

public record PickerSessionRequest
{
    [JsonPropertyName("pickingConfig")]
    public PickerSessionPickingConfig? PickingConfig { get; init; }
}

public record PickerSessionPickingConfig
{
    [JsonPropertyName("maxItemCount")]
    public long? MaxItemCount { get; init; }
}
