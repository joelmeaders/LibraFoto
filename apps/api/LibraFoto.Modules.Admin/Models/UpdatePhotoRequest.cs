namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to update a photo's metadata.
/// </summary>
public record UpdatePhotoRequest(
    string? Filename,
    string? Location,
    DateTime? DateTaken
);
