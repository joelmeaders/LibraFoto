namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to create a new album.
/// </summary>
public record CreateAlbumRequest(
    string Name,
    string? Description = null,
    long? CoverPhotoId = null
);
