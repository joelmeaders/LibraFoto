using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LibraFoto.Data.Enums;

namespace LibraFoto.Data.Entities;

/// <summary>
/// Represents a storage provider configuration for photo sources.
/// </summary>
public class StorageProvider
{
    /// <summary>
    /// Primary key. SQLite INTEGER PRIMARY KEY for auto-increment.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Type of storage provider.
    /// </summary>
    public StorageProviderType Type { get; set; }

    /// <summary>
    /// Display name for this storage provider instance.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// JSON-serialized configuration (OAuth tokens, folder paths, etc.).
    /// Sensitive data should be encrypted before storage.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Date of the last successful sync with this provider.
    /// </summary>
    public DateTime? LastSyncDate { get; set; }

    // Navigation properties

    /// <summary>
    /// Photos imported from this provider.
    /// </summary>
    public ICollection<Photo> Photos { get; set; } = [];
}
