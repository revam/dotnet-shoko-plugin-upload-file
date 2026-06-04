using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.UploadFile.Configuration;
using Shoko.Plugin.UploadFile.Models;

namespace Shoko.Plugin.UploadFile.Services;

/// <summary>
/// Core business logic for area management and file uploads.
/// Handles configuration persistence, destination path computation,
/// checksum validation via <see cref="IncrementalHash"/>, and concurrency
/// control via <see cref="AsyncKeyedLocker{T}"/> keyed on destination paths.
/// </summary>
public partial class UploadService(
    ILogger<UploadService> logger,
    ConfigurationProvider<PluginConfiguration> configProvider,
    IVideoService videoService)
{
    private readonly ILogger<UploadService> _logger = logger;
    private readonly ConfigurationProvider<PluginConfiguration> _configProvider = configProvider;
    private readonly IVideoService _videoService = videoService;

    /// <summary>
    /// Async keyed lock used to serialize concurrent uploads to the same
    /// destination file path. When one upload is in progress for a given
    /// path, subsequent uploads to that path will await the lock before
    /// checking for file existence.
    /// Pool size of 1 ensures single concurrency per key.
    /// </summary>
    private readonly AsyncKeyedLocker<string> _locker = new(o => o.MaxCount = 1);

    /// <summary>
    /// Validates area names against the allowed hex character pattern.
    /// Pattern: ^[a-f](?:[a-f0-9]*[a-f])?$
    /// Matches hex strings where the first and last characters are letters a-f.
    /// </summary>
    [GeneratedRegex(@"^[a-f](?:[a-z0-9]*[a-z])?$")]
    public static partial Regex AreaNamePattern();

    /// <summary>
    /// Validates checksum strings provided in upload requests.
    /// Acceptable formats: sha1:&lt;hash&gt;, sha256:&lt;hash&gt;, md5:&lt;hash&gt;
    /// where the hash portion is lowercase hexadecimal.
    /// </summary>
    [GeneratedRegex(@"^(sha1|sha256|md5):([a-f0-9]+)$", RegexOptions.IgnoreCase)]
    public static partial Regex ChecksumPattern();

    /// <summary>
    /// Returns all configured areas with their managed folder names resolved
    /// from Shoko's current folder list. If a folder has been deleted, the
    /// cached folder name from the area config is used as a fallback.
    /// </summary>
    /// <returns>A list of area summaries with resolved display names.</returns>
    public List<Area> ListAreas()
    {
        var config = _configProvider.Load();
        var folderNames = _videoService.GetAllManagedFolders()
            .ToDictionary(f => f.ID, f => f.Name);

        return [.. config.Areas.Select(a => new Area
        {
            Name = a.Name,
            ManagedFolderName = folderNames.GetValueOrDefault(a.ManagedFolderID, a.ManagedFolderName),
            RelativePath = a.RelativePath,
        })];
    }

    /// <summary>
    /// Creates a new upload area and persists it to the plugin configuration.
    /// The area name is lowercased for consistency. The managed folder name is
    /// resolved at creation time and cached in the area config for display.
    /// </summary>
    /// <param name="request">The area creation parameters.</param>
    /// <returns>The created area summary with resolved folder name.</returns>
    public Area CreateArea(CreateAreaBody request)
    {
        var config = _configProvider.Load();
        var folders = _videoService.GetAllManagedFolders();
        var folder = folders.First(f => f.ID == request.ManagedFolderID);

        var area = new AreaConfiguration
        {
            Name = request.Name.ToLowerInvariant(),
            ManagedFolderID = request.ManagedFolderID,
            ManagedFolderName = folder.Name,
            RelativePath = request.RelativePath?.Trim('/') ?? string.Empty,
        };

        config.Areas.Add(area);
        _configProvider.Save(config);

        return new Area
        {
            Name = area.Name,
            ManagedFolderName = folder.Name,
            RelativePath = area.RelativePath,
        };
    }

    /// <summary>
    /// Removes an area definition from the plugin configuration by name.
    /// Comparison is case-insensitive. Already uploaded files on disk are not
    /// affected by this operation.
    /// </summary>
    /// <param name="name">The area name to remove.</param>
    public void DeleteArea(string name)
    {
        var config = _configProvider.Load();
        config.Areas.RemoveAll(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        _configProvider.Save(config);
    }

    /// <summary>
    /// Computes the absolute destination path for an uploaded file within a
    /// managed folder. When <paramref name="useSubdirectories"/> is true, the
    /// path includes hash-based subdirectory nesting using the first four
    /// characters of the checksum hash (e.g., ab/cd/filename.mkv).
    /// </summary>
    /// <param name="managedFolderPath">Absolute path of the managed folder.</param>
    /// <param name="relativePath">Relative subdirectory within the folder, may be empty.</param>
    /// <param name="fileName">Original file name, sanitized for the filesystem.</param>
    /// <param name="checksumHeader">Checksum header used for subdirectory generation.</param>
    /// <param name="useSubdirectories">
    /// When true, nest files under hash[0..2]/hash[2..4]/ subdirectories.
    /// When false, place files directly in the area root.
    /// </param>
    /// <returns>The full absolute destination path.</returns>
    public string BuildDestinationPath(
        string managedFolderPath,
        string relativePath,
        string fileName,
        string checksumHeader,
        bool useSubdirectories)
    {
        var basePath = string.IsNullOrEmpty(relativePath)
            ? managedFolderPath
            : Path.Combine(managedFolderPath, relativePath);

        if (useSubdirectories)
        {
            var match = ChecksumPattern().Match(checksumHeader);
            var hash = match.Groups[2].Value;
            var subDir = hash.Length >= 4
                ? Path.Combine(hash[..2], hash[2..4])
                : hash;
            return Path.Combine(basePath, subDir, SanitizeFileName(fileName));
        }

        return Path.Combine(basePath, SanitizeFileName(fileName));
    }

    /// <summary>
    /// Uploads a file stream to the destination path with checksum validation
    /// and concurrency locking. The file is first streamed to a temporary file
    /// in the same directory while its hash is computed in a single pass.
    /// Only after the checksum is verified is the temp file atomically renamed
    /// to the final destination.
    /// </summary>
    /// <param name="fileStream">The file data stream from the HTTP request.</param>
    /// <param name="destPath">Absolute destination path for the final file.</param>
    /// <param name="checksumHeader">Checksum header in sha1:, sha256:, or md5: format.</param>
    /// <param name="fileLength">Total file size in bytes.</param>
    /// <returns>
    /// A tuple containing the <see cref="UploadResult"/> on success or an error
    /// message string on failure. The caller should check error before using result.
    /// </returns>
    public async Task<(UploadResult Result, string? Error)> UploadAsync(
        Stream fileStream,
        string destPath,
        string checksumHeader,
        long fileLength)
    {
        var match = ChecksumPattern().Match(checksumHeader);
        if (!match.Success)
            return (null!, "Invalid checksum format. Use sha1:, sha256:, or md5: prefix.");

        var hashType = match.Groups[1].Value.ToLowerInvariant();
        var expectedHash = match.Groups[2].Value.ToLowerInvariant();

        var algoName = hashType switch
        {
            "md5" => HashAlgorithmName.MD5,
            "sha1" => HashAlgorithmName.SHA1,
            "sha256" => HashAlgorithmName.SHA256,
            _ => HashAlgorithmName.SHA256,
        };

        using (await _locker.LockAsync(destPath))
        {
            if (File.Exists(destPath))
                return (null!, "File already exists.");

            var tempPath = destPath + ".upload.tmp";
            try
            {
                var (computedHash, size) = await HashAndCopyAsync(fileStream, tempPath, algoName);

                if (fileLength > 0 && size != fileLength)
                {
                    TryDeleteFile(tempPath);
                    return (null!, $"Size mismatch. Expected {fileLength} bytes, received {size} bytes.");
                }

                if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(tempPath);
                    return (null!, $"Checksum mismatch. Expected {expectedHash}, computed {computedHash}.");
                }

                var dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.Move(tempPath, destPath);

                await _videoService.NotifyVideoFileChangeDetected(destPath);

                _logger.LogInformation("Uploaded file to {Path} ({Size} bytes, {Hash})", destPath, size, checksumHeader);

                return (new UploadResult
                {
                    Path = destPath,
                    Size = size,
                    Checksum = checksumHeader,
                }, null);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }
    }

    /// <summary>
    /// Copies a stream to a temporary file while computing its cryptographic
    /// hash in a single forward pass using <see cref="IncrementalHash"/>.
    /// This avoids buffering the entire file in memory or reading it twice.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="tempPath">Path to the temporary file to write to.</param>
    /// <param name="algoName">The hash algorithm to use (MD5, SHA1, or SHA256).</param>
    /// <returns>A tuple of the hex-encoded hash and the total number of bytes copied.</returns>
    private static async Task<(string Hash, long Size)> HashAndCopyAsync(
        Stream source,
        string tempPath,
        HashAlgorithmName algoName)
    {
        using var hash = IncrementalHash.CreateHash(algoName);
        using var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write);

        var buffer = new byte[65536];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            hash.AppendData(buffer.AsSpan(0, read));
            dest.Write(buffer, 0, read);
            total += read;
        }

        var hashBytes = hash.GetHashAndReset();
        return (Convert.ToHexString(hashBytes).ToLowerInvariant(), total);
    }

    /// <summary>
    /// Replaces characters in the file name that are invalid for the current
    /// filesystem with underscores. If the result is empty, returns "unnamed".
    /// </summary>
    /// <param name="fileName">The original file name to sanitize.</param>
    /// <returns>A filesystem-safe file name.</returns>
    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(fileName.Select(c =>
            invalid.Contains(c) ? '_' : c));
        return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
    }

    /// <summary>
    /// Attempts to delete a file at the given path, swallowing any exceptions.
    /// Used for cleanup of temporary files when an upload fails partway through.
    /// </summary>
    /// <param name="path">The absolute path to delete.</param>
    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { /* best effort */ }
    }
}
