namespace LibraFoto.Modules.Auth.Models
{
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
}
