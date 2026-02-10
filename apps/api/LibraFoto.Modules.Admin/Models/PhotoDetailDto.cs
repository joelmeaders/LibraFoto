using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Full photo details including related albums and tags.
/// </summary>
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

/// <summary>
/// Minimal album info for photo detail view.
/// </summary>
public record AlbumSummaryDto(
    long Id,
    string Name
);

/// <summary>
/// Minimal tag info for photo detail view.
/// </summary>
public record TagSummaryDto(
    long Id,
    string Name,
    string? Color
);
