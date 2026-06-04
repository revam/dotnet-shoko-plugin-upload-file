namespace Shoko.Plugin.UploadFile.Models;

/// <summary>
/// Summary of a managed folder as returned by the managed-folders endpoint.
/// Used by API consumers to select the correct folder ID when creating areas.
/// </summary>
public class ManagedFolder
{
    /// <summary>
    /// Managed folder ID. Used as <see cref="CreateAreaBody.ManagedFolderID"/>.
    /// </summary>
    public required int ID { get; set; }

    /// <summary>
    /// Display name of the managed folder as configured in Shoko.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Absolute path of the managed folder on disk.
    /// </summary>
    public required string Path { get; set; }
}
