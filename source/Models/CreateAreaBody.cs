using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.UploadFile.Models;

/// <summary>
/// Request body for creating a new upload area via the API.
/// All fields are required for a valid area definition.
/// </summary>
public class CreateAreaBody
{
    /// <summary>
    /// Area name.
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-z](?:[a-z0-9]*[a-z])?$")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ID of the managed folder to use as the storage backend.
    /// Must reference an existing managed folder from Shoko's configuration.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int ManagedFolderID { get; set; }

    /// <summary>
    /// Subdirectory within the managed folder for uploaded files.
    /// Leading and trailing slashes are stripped during creation.
    /// </summary>
    public string? RelativePath { get; set; }
}
