using System.ComponentModel.DataAnnotations;

namespace LibraFoto.Modules.Auth.Models;

/// <summary>
/// Request model for initial setup - creating the first admin user.
/// </summary>
public record SetupRequest(
    [Required]
    [EmailAddress]
    [StringLength(255)]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 6)]
    string Password
);

/// <summary>
/// Response model for setup status check.
/// </summary>
public record SetupStatusResponse(
    bool IsSetupRequired,
    string? Message
);
