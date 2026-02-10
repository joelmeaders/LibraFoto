namespace LibraFoto.Shared.Configuration;

/// <summary>
/// Provides cross-platform default paths for LibraFoto data storage.
/// </summary>
public static class LibraFotoDefaults
{
    /// <summary>
    /// Gets the default data directory for LibraFoto.
    /// </summary>
    /// <returns>
    /// Platform-specific default directory:
    /// - Windows/Mac: %LOCALAPPDATA%\LibraFoto or ~/Library/Application Support/LibraFoto
    /// - Linux: ~/.local/share/LibraFoto
    /// - Docker (detected by /app or /data): ./data
    /// </returns>
    public static string GetDefaultDataDirectory()
    {
        // Check if running in Docker (common container paths)
        if (Directory.Exists("/app") || Directory.Exists("/data"))
        {
            return "./data";
        }

        // Get platform-specific application data directory
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        // On Linux, LocalApplicationData points to ~/.local/share
        // On Windows, it points to %LOCALAPPDATA%
        // On Mac, it points to ~/Library/Application Support
        return Path.Combine(appDataPath, "LibraFoto");
    }

    /// <summary>
    /// Gets the default database file path.
    /// </summary>
    /// <returns>Full path to librafoto.db in the default data directory.</returns>
    public static string GetDefaultDatabasePath()
    {
        var dataDir = GetDefaultDataDirectory();

        // Ensure directory exists
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        return Path.Combine(dataDir, "librafoto.db");
    }

    /// <summary>
    /// Gets the default photos storage path.
    /// </summary>
    /// <returns>Full path to photos directory in the default data directory.</returns>
    public static string GetDefaultPhotosPath()
    {
        var dataDir = GetDefaultDataDirectory();
        var photosPath = Path.Combine(dataDir, "photos");

        // Ensure directory exists
        if (!Directory.Exists(photosPath))
        {
            Directory.CreateDirectory(photosPath);
        }

        return photosPath;
    }
}
