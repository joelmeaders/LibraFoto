using System.ComponentModel.DataAnnotations;
using LibraFoto.Data.Enums;

namespace LibraFoto.Modules.Auth.Models
{
    /// <summary>
    /// Request model for creating a new user.
    /// </summary>
    public record CreateUserRequest(
        [Required]
        [EmailAddress]
        [StringLength(255)]
        string Email,

        [Required]
        [StringLength(100, MinimumLength = 6)]
        string Password,

        [Required]
        UserRole Role
    );

    /// <summary>
    /// Request model for updating an existing user.
    /// </summary>
    public record UpdateUserRequest(
        [EmailAddress]
        [StringLength(255)]
        string? Email,

        [StringLength(100, MinimumLength = 6)]
        string? Password,

        UserRole? Role
    );
}
