using System.Text.Json.Serialization;

namespace LibraFoto.Modules.Storage.Models;

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

internal record PickerSessionResponse
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

internal record PickerPollingConfigResponse
{
    [JsonPropertyName("pollInterval")]
    public string? PollInterval { get; init; }

    [JsonPropertyName("timeoutIn")]
    public string? TimeoutIn { get; init; }
}

internal record PickedMediaItemsResponse
{
    [JsonPropertyName("mediaItems")]
    public List<PickedMediaItemResponse>? MediaItems { get; init; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; init; }
}

internal record PickedMediaItemResponse
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

internal record PickedMediaFileResponse
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

internal record PickedMediaFileMetadataResponse
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

internal record PickedPhotoMetadataResponse
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

internal record PickedVideoMetadataResponse
{
    [JsonPropertyName("fps")]
    public double? Fps { get; init; }

    [JsonPropertyName("processingStatus")]
    public string? ProcessingStatus { get; init; }
}

internal record PickerSessionRequest
{
    [JsonPropertyName("pickingConfig")]
    public PickerSessionPickingConfig? PickingConfig { get; init; }
}

internal record PickerSessionPickingConfig
{
    [JsonPropertyName("maxItemCount")]
    public long? MaxItemCount { get; init; }
}
