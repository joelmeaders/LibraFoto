namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Request to cache a file downloaded from a storage provider.
/// </summary>
public record CacheFileRequest
{
    public required string FileHash { get; init; }
    public required string OriginalUrl { get; init; }
    public long ProviderId { get; init; }
    public string? ProviderFileId { get; init; }
    public string? PickerSessionId { get; init; }
    public required Stream FileStream { get; init; }
    public required string ContentType { get; init; }
}
