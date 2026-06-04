using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Video.Services;

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
    /// </summary>
    [Key]
    [RegularExpression(@"^[a-z](?:[a-z0-9]*[a-z])?$")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ID of the managed folder that files will be uploaded into.
    /// Must reference an existing managed folder returned by <see cref="IVideoService"/>.
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
