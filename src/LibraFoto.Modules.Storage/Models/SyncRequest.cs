namespace LibraFoto.Modules.Storage.Models;

/// <summary>
/// Request parameters for a sync operation.
/// </summary>
public record SyncRequest
{
    /// <summary>
    /// Whether to perform a full sync (rescan all files) or incremental.
    /// </summary>
    public bool FullSync { get; init; } = false;

    /// <summary>
    /// Whether to delete local records for files that no longer exist in the provider.
    /// </summary>
    public bool RemoveDeleted { get; init; } = true;

    /// <summary>
    /// Whether to skip files that are already in the database.
    /// </summary>
    public bool SkipExisting { get; init; } = true;

    /// <summary>
    /// Maximum number of files to process in this sync (0 = unlimited).
    /// </summary>
    public int MaxFiles { get; init; } = 0;

    /// <summary>
    /// Folder ID to sync (null for root/all folders).
    /// </summary>
    public string? FolderId { get; init; }

    /// <summary>
    /// Whether to sync recursively into subfolders.
    /// </summary>
    public bool Recursive { get; init; } = true;
}
