namespace Shoko.Plugin.UploadFile.Models;

/// <summary>
/// Plugin settings exposed via the API.
/// Controls the master enable switch and the file placement strategy.
/// </summary>
public class Settings
{
    /// <summary>
    /// Master switch to enable or disable the plugin.
    /// When disabled, all upload endpoints return 503 Service Unavailable.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Controls file placement within the area's managed folder.
    /// When true, files are placed in hash-based subdirectories
    /// (e.g., ab/cd/filename.mkv) instead of directly in the area root.
    /// </summary>
    public bool UseSubdirectories { get; set; }
}
