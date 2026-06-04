namespace Shoko.Plugin.UploadFile.Models;

/// <summary>
/// Result returned after a successful file upload.
/// Includes the final absolute path, file size, and the checksum
/// header that was provided in the upload request.
/// </summary>
public class UploadResult
{
    /// <summary>
    /// Absolute path on disk where the file was written.
    /// Includes the managed folder path and any configured subdirectories.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// File size in bytes as computed during the streaming copy.
    /// </summary>
    public required long Size { get; set; }

    /// <summary>
    /// Checksum header exactly as provided in the upload request
    /// (e.g. sha256:abc123...), echoed back for confirmation.
    /// </summary>
    public required string Checksum { get; set; }
}
