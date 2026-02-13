using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Storage.Features.Shared;

public record PickerPollingConfig
{
    public string? PollInterval { get; init; }
    public string? TimeoutIn { get; init; }
}

public record PickerSessionDto
{
    public required string SessionId { get; init; }
    public required string PickerUri { get; init; }
    public bool MediaItemsSet { get; init; }
    public DateTime? ExpireTime { get; init; }
    public PickerPollingConfig? PollingConfig { get; init; }
}

public record PickedMediaItemDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string? MimeType { get; init; }
    public string? Filename { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? CreateTime { get; init; }
    public string? VideoProcessingStatus { get; init; }
    public string? ThumbnailUrl { get; init; }
}

public record StorageProviderDto
{
    public long Id { get; init; }
    public StorageProviderType Type { get; init; }
    public required string Name { get; init; }
    public bool IsEnabled { get; init; }
    public bool SupportsUpload { get; init; }
    public bool SupportsWatch { get; init; }
    public DateTime? LastSyncDate { get; init; }
    public int PhotoCount { get; init; }
    public bool? IsConnected { get; init; }
    public string? StatusMessage { get; init; }
}

public record CreateStorageProviderRequest
{
    public StorageProviderType Type { get; init; }
    public required string Name { get; init; }
    public string? Configuration { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public record UpdateStorageProviderRequest
{
    public string? Name { get; init; }
    public string? Configuration { get; init; }
    public bool? IsEnabled { get; init; }
}

public record UploadRequest
{
    public long? AlbumId { get; init; }
    public List<string>? Tags { get; init; }
    public string? CustomFilename { get; init; }
    public bool Overwrite { get; init; } = false;
}

public record GuestUploadRequest
{
    public required string LinkId { get; init; }
    public string? Message { get; init; }
}
