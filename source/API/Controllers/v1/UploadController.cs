using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.UploadFile.Configuration;
using Shoko.Plugin.UploadFile.Models;
using Shoko.Plugin.UploadFile.Services;

namespace Shoko.Plugin.UploadFile.API.Controllers.v1;

/// <summary>
/// API controller for managing upload areas and uploading files.
/// All endpoints require authentication and admin privileges.
/// Endpoints are exposed under /api/plugin/UploadFile/v1/ and can be
/// consumed by any HTTP client with a valid Shoko admin API token.
/// </summary>
/// <param name="uploadService">Core business logic for area management and file uploads.</param>
/// <param name="configProvider">Typed configuration provider for reading and saving plugin settings.</param>
/// <param name="videoService">Shoko video service, used to enumerate managed folders.</param>
[Authorize("admin")]
[ApiController]
[Route("/api/plugin/UploadFile/v1")]
public class UploadController(
    UploadService uploadService,
    ConfigurationProvider<PluginConfiguration> configProvider,
    IVideoService videoService) : ControllerBase
{
    private readonly UploadService _uploadService = uploadService;

    private readonly ConfigurationProvider<PluginConfiguration> _configProvider = configProvider;

    private readonly IVideoService _videoService = videoService;

    /// <summary>
    /// Returns the current plugin settings, including the master enable switch
    /// and the subdirectory file placement mode.
    /// </summary>
    /// <returns>The current settings values.</returns>
    [HttpGet("Settings")]
    public ActionResult<Settings> GetSettings()
    {
        var config = _configProvider.Load();
        return new Settings
        {
            Enabled = config.Enabled,
            UseSubdirectories = config.UseSubdirectories,
        };
    }

    /// <summary>
    /// Updates plugin settings. Only the fields provided in the request body
    /// are modified; omitted fields retain their current values.
    /// </summary>
    /// <param name="settings">The settings values to update.</param>
    /// <returns>The updated settings after persistence.</returns>
    [HttpPut("Settings")]
    public ActionResult<Settings> UpdateSettings([FromBody] Settings settings)
    {
        if (settings is null)
            return BadRequest("Request body is required.");

        var config = _configProvider.Load();
        config.Enabled = settings.Enabled;
        config.UseSubdirectories = settings.UseSubdirectories;
        _configProvider.Save(config);

        return new Settings
        {
            Enabled = config.Enabled,
            UseSubdirectories = config.UseSubdirectories,
        };
    }

    /// <summary>
    /// Lists all configured upload areas with their resolved managed folder
    /// display names. The folder name is resolved from Shoko's current folder
    /// list; if a folder has been deleted, the cached name is shown instead.
    /// </summary>
    /// <returns>A list of area summaries.</returns>
    /// <response code="200">Areas returned successfully.</response>
    /// <response code="403">User is not an administrator.</response>
    [HttpGet("Area")]
    public ActionResult<List<Area>> ListAreas()
    {
        return _uploadService.ListAreas();
    }

    /// <summary>
    /// Creates a new upload area. Validates the area name against the hex
    /// character pattern, checks that the managed folder exists, and ensures
    /// no duplicate area name exists. The created area is persisted to the
    /// plugin configuration.
    /// </summary>
    /// <param name="body">The area configuration including name, folder ID, and relative path.</param>
    /// <returns>The created area summary with location header pointing to the areas list.</returns>
    /// <response code="201">Area created successfully.</response>
    /// <response code="400">Validation failed: invalid name, missing folder, or bad request body.</response>
    /// <response code="403">User is not an administrator.</response>
    /// <response code="409">An area with the same name already exists.</response>
    [HttpPost("Area")]
    public ActionResult<Area> CreateArea([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] CreateAreaBody body)
    {
        if (_videoService.GetManagedFolderByID(body.ManagedFolderID) is null)
            return BadRequest("Managed folder not found.");

        var config = _configProvider.Load();
        if (config.Areas.Any(a => string.Equals(a.Name, body.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict("An area with this name already exists.");

        var area = _uploadService.CreateArea(body);
        return CreatedAtAction(nameof(ListAreas), area);
    }

    /// <summary>
    /// Deletes an upload area definition by name. The lookup is case-insensitive.
    /// Already uploaded files on disk are not removed; only the area
    /// configuration entry is deleted from the plugin settings.
    /// </summary>
    /// <param name="name">The area name to delete.</param>
    /// <returns>An object confirming the deleted area name.</returns>
    /// <response code="200">Area deleted successfully.</response>
    /// <response code="400">Area name was not provided.</response>
    /// <response code="403">User is not an administrator.</response>
    /// <response code="404">No area with the given name exists.</response>
    [HttpDelete("Area/{name}")]
    public ActionResult DeleteArea(string name)
    {
        if (string.IsNullOrEmpty(name))
            return BadRequest("Area name is required.");

        var config = _configProvider.Load();
        var area = config.Areas.Find(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        if (area is null)
            return NotFound("Area not found.");

        _uploadService.DeleteArea(name);
        return Ok(new { deleted = name });
    }

    /// <summary>
    /// Uploads a file to the specified area. The request must be sent as
    /// multipart/form-data with the file binary in the "file" field and a
    /// checksum string in the "checksum" field. The checksum must use the
    /// format sha1:&lt;hash&gt;, sha256:&lt;hash&gt;, or md5:&lt;hash&gt;.
    /// The file is validated against the checksum before being written to
    /// the destination path within the area's managed folder.
    /// </summary>
    /// <param name="name">The target area name.</param>
    /// <param name="file">The file to upload, provided as form data.</param>
    /// <param name="checksum">Checksum in sha1:, sha256:, or md5: format.</param>
    /// <returns>The upload result with the final path, size, and echoed checksum.</returns>
    /// <response code="201">File uploaded successfully.</response>
    /// <response code="400">File missing, nameless, checksum missing or invalid.</response>
    /// <response code="403">User is not an administrator.</response>
    /// <response code="404">The specified area was not found.</response>
    /// <response code="409">A file already exists at the destination path.</response>
    /// <response code="422">Checksum validation failed; the computed hash does not match.</response>
    /// <response code="503">The plugin is disabled via configuration.</response>
    [HttpPost("Area/{name}/Upload")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<UploadResult>> UploadToArea(
        string name,
        IFormFile file,
        [FromForm] string checksum)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (string.IsNullOrEmpty(file.FileName))
            return BadRequest("File must have a name.");

        if (string.IsNullOrEmpty(checksum))
            return BadRequest("Checksum is required.");

        var config = _configProvider.Load();

        if (!config.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Plugin is disabled.");

        var area = config.Areas.Find(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        if (area is null)
            return NotFound("Area not found.");

        var folders = _videoService.GetAllManagedFolders();
        var folder = folders.FirstOrDefault(f => f.ID == area.ManagedFolderID);

        if (folder is null)
        {
            _uploadService.DeleteArea(name);
            return Ok(new
            {
                area_deleted = name,
                message = "Referenced managed folder no longer exists.",
            });
        }

        var destPath = _uploadService.BuildDestinationPath(
            folder.Path,
            area.RelativePath,
            file.FileName,
            checksum,
            config.UseSubdirectories);

        var (result, error) = await _uploadService.UploadAsync(
            file.OpenReadStream(), destPath, checksum, file.Length);

        if (error is not null)
        {
            if (error.StartsWith("File already exists"))
                return Conflict(error);

            if (error.StartsWith("Checksum mismatch"))
                return UnprocessableEntity(new { error });

            if (error.StartsWith("Invalid checksum"))
                return BadRequest(error);

            return BadRequest(error);
        }

        return Created(result.Path, result);
    }

    /// <summary>
    /// Lists all managed folders currently configured in Shoko.
    /// Each folder entry includes its ID (for use when creating areas),
    /// display name, and absolute path on disk.
    /// </summary>
    /// <returns>A list of managed folder summaries.</returns>
    /// <response code="200">Managed folders returned successfully.</response>
    /// <response code="403">User is not an administrator.</response>
    [HttpGet("ManagedFolder")]
    public ActionResult<List<ManagedFolder>> ListManagedFolders()
    {
        return _videoService.GetAllManagedFolders()
            .Select(f => new ManagedFolder
            {
                ID = f.ID,
                Name = f.Name,
                Path = f.Path,
            })
            .ToList();
    }
}
