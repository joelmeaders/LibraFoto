using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LibraFoto.Data.Enums;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a user of the LibraFoto application.
/// </summary>
public class User
{
    /// <summary>
    /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// User's email address (used for login and notifications).
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password (using a secure hashing algorithm like Argon2 or bcrypt).
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// User's role determining their permissions.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Guest;

    /// <summary>
    /// Date the user account was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date of the user's last login.
    /// </summary>
    public DateTime? LastLogin { get; set; }

    // Navigation properties

    /// <summary>
    /// Guest links created by this user.
    /// </summary>
    public ICollection<GuestLink> CreatedGuestLinks { get; set; } = [];
}
