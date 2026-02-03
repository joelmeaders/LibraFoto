namespace LibraFoto.Modules.Admin.Models;

/// <summary>
/// Tag information for list and detail views.
/// </summary>
public record TagDto(
    long Id,
    string Name,
    string? Color,
    int PhotoCount
);
