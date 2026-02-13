using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Services.Shared;

/// <summary>
/// Request model for creating a guest upload link.
/// </summary>
public record CreateGuestLinkRequest(
    [Required]
    [StringLength(100, MinimumLength = 1)]
    string Name,

    DateTime? ExpiresAt,

    [Range(1, 1000)]
    int? MaxUploads,

    long? TargetAlbumId
);

/// <summary>
/// Guest link data transfer object for display.
/// </summary>
public record GuestLinkDto(
    string Id,
    string Name,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    int? MaxUploads,
    int CurrentUploads,
    long? TargetAlbumId,
    string? TargetAlbumName,
    long CreatedByUserId,
    string CreatedByUsername,
    bool IsActive
);

/// <summary>
/// Response model for guest link validation.
/// </summary>
public record GuestLinkValidationResponse(
    bool IsValid,
    string? Name,
    string? TargetAlbumName,
    int? RemainingUploads,
    string? Message
);
