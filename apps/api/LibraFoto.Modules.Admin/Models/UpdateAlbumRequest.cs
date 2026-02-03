namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to update an album.
/// </summary>
public record UpdateAlbumRequest(
    string? Name = null,
    string? Description = null,
    long? CoverPhotoId = null,
    int? SortOrder = null
);
