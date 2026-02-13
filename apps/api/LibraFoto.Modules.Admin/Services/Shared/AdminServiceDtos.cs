using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Admin.Services.Shared;

public record AlbumDto(
    long Id,
    string Name,
    string? Description,
    long? CoverPhotoId,
    string? CoverPhotoThumbnail,
    DateTime DateCreated,
    int SortOrder,
    int PhotoCount
);

public record BulkOperationResult(
    int SuccessCount,
    int FailedCount,
    string[] Errors
);

public record CreateAlbumRequest(
    string Name,
    string? Description = null,
    long? CoverPhotoId = null
);

public record CreateTagRequest(
    string Name,
    string? Color = null
);

public record PhotoCountDto(int Count);

public record PhotoDetailDto(
    long Id,
    string Filename,
    string OriginalFilename,
    string FilePath,
    string? ThumbnailPath,
    int Width,
    int Height,
    long FileSize,
    MediaType MediaType,
    double? Duration,
    DateTime? DateTaken,
    DateTime DateAdded,
    string? Location,
    double? Latitude,
    double? Longitude,
    long? ProviderId,
    string? ProviderName,
    AlbumSummaryDto[] Albums,
    TagSummaryDto[] Tags
);

public record AlbumSummaryDto(
    long Id,
    string Name
);

public record TagSummaryDto(
    long Id,
    string Name,
    string? Color
);

public record PhotoFilterRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public long? AlbumId { get; init; }
    public long? TagId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public MediaType? MediaType { get; init; }
    public string? Search { get; init; }
    public string SortBy { get; init; } = "DateAdded";
    public string SortDirection { get; init; } = "desc";
}

public record PhotoListDto(
    long Id,
    string Filename,
    string ThumbnailPath,
    int Width,
    int Height,
    MediaType MediaType,
    DateTime? DateTaken,
    DateTime DateAdded,
    string? Location,
    int AlbumCount,
    int TagCount
);

public record PhotoOrder(
    long PhotoId,
    int SortOrder
);

public record SystemInfoResponse
{
    public required string Version { get; init; }
    public string? CommitHash { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? LatestVersion { get; init; }
    public int? CommitsBehind { get; init; }
    public IReadOnlyList<string>? Changelog { get; init; }
    public DateTime? LastChecked { get; init; }
    public TimeSpan Uptime { get; init; }
    public bool IsDocker { get; init; }
    public required string Environment { get; init; }
}

public record UpdateCheckResponse
{
    public bool UpdateAvailable { get; init; }
    public required string CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public int CommitsBehind { get; init; }
    public IReadOnlyList<string> Changelog { get; init; } = [];
    public string? Error { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}

public record UpdateTriggerResponse(string Message, int EstimatedDowntimeSeconds);

public record TagDto(
    long Id,
    string Name,
    string? Color,
    int PhotoCount
);

public record UpdateAlbumRequest(
    string? Name = null,
    string? Description = null,
    long? CoverPhotoId = null,
    int? SortOrder = null
);

public record UpdatePhotoRequest(
    string? Filename,
    string? Location,
    DateTime? DateTaken
);

public record UpdateTagRequest(
    string? Name = null,
    string? Color = null
);
