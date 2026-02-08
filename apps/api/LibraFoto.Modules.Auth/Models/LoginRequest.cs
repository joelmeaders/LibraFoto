using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Models
{
    /// <summary>
    /// Request model for user login.
    /// </summary>
    public record LoginRequest(
        [Required]
        [EmailAddress]
        [StringLength(255)]
        string Email,

        [Required]
        [StringLength(100, MinimumLength = 6)]
        string Password
    );
}
