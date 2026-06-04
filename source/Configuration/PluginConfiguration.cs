using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.UploadFile.Configuration;

/// <summary>
/// Plugin settings persisted by Shoko's configuration system.
/// Rendered in the Shoko UI under a minimal section.
/// Areas are hidden behind the "Advanced" toggle and managed through the API.
/// </summary>
[Section(DisplaySectionType.Minimal)]
public class PluginConfiguration : IHiddenConfiguration
{
    /// <summary>
    /// Master switch to enable or disable the plugin.
    /// When disabled, upload endpoints return 503 Service Unavailable.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Controls file placement within the area's managed folder.
    /// When enabled, files are placed in hash-based subdirectories
    /// (e.g., ab/cd/filename.mkv) instead of directly in the area root.
    /// </summary>
    [Display(Name = "Use Subdirectories")]
    public bool UseSubdirectories { get; set; }

    /// <summary>
    /// Named upload destinations, each referencing a managed folder and relative path.
    /// Managed through the API endpoints; not intended for direct UI editing.
    /// </summary>
    [Visibility(Advanced = true)]
    [List(ListType = DisplayListType.ComplexInline)]
    public List<AreaConfiguration> Areas { get; set; } = [];
}
