namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Album information for list and detail views.
/// </summary>
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
