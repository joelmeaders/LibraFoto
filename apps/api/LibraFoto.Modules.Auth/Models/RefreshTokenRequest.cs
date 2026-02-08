using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Models
{
    /// <summary>
    /// Request model for refreshing an access token.
    /// </summary>
    public record RefreshTokenRequest(
        [Required]
        string RefreshToken
    );
}
