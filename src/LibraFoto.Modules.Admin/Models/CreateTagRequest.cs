namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Request to create a new tag.
/// </summary>
public record CreateTagRequest(
    string Name,
    string? Color = null
);
