using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Models
{
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
}
