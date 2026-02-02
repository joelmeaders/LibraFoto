namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to remove photos from a tag.
/// </summary>
public record RemovePhotosFromTagRequest(
    long[] PhotoIds
);
