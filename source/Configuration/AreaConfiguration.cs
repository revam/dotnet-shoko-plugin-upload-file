namespace Shoko.Plugin.UploadFile.Configuration;

/// <summary>
/// A named upload destination that pairs a managed folder with a relative path.
/// Instances are created by the API and stored in <see cref="PluginConfiguration.Areas"/>.
/// Properties are init-only; areas are immutable after creation.
/// </summary>
public class AreaConfiguration
{
    /// <summary>
    /// Unique area identifier used in API routes.
    /// Must match the regex pattern ^[a-f](?:[a-f0-9]*[a-f])?$: a hex string where
    /// the first and last characters are letters a-f, minimum length 1.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ID of the managed folder that files will be uploaded into.
    /// Must reference an existing managed folder returned by <see cref="Shoko.Abstractions.Video.Services.IVideoService"/>.
    /// </summary>
    public int ManagedFolderID { get; init; }

    /// <summary>
    /// Display name of the managed folder, resolved from Shoko's managed folder list
    /// at the time the area is created. May become stale if the folder is renamed.
    /// </summary>
    public string ManagedFolderName { get; init; } = string.Empty;

    /// <summary>
    /// Relative subdirectory within the managed folder where uploaded files are placed.
    /// Leading and trailing slashes are stripped during creation.
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;
}
