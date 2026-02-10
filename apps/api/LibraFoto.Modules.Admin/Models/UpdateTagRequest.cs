namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to update a tag.
/// </summary>
public record UpdateTagRequest(
    string? Name = null,
    string? Color = null
);
