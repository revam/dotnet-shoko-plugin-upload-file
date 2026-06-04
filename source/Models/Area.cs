namespace Shoko.Plugin.UploadFile.Models;

/// <summary>
/// Summary of a configured upload area returned by the API.
/// The managed folder name is resolved from Shoko's folder list at query time.
/// </summary>
public class Area
{
    /// <summary>
    /// Area name as it appears in the configuration.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Resolved display name of the referenced managed folder.
    /// Shows the raw value from the configuration's cached folder name if the
    /// folder is no longer available.
    /// </summary>
    public required string ManagedFolderName { get; set; }

    /// <summary>
    /// Subdirectory within the managed folder where files are placed.
    /// </summary>
    public required string RelativePath { get; set; }
}
