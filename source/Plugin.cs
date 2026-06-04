using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.UploadFile.Services;

namespace Shoko.Plugin.UploadFile;

/// <summary>
/// Entry point for the Upload File plugin.
/// Implements the plugin identity contract and service registration so that
/// Shoko's plugin manager can discover and initialize this assembly at startup.
/// </summary>
public class Plugin : IPlugin, IPluginServiceRegistration
{
    /// <summary>
    /// Plugin identifier GUID.
    /// This value MUST match the "id" field in manifest.json; otherwise the
    /// plugin loader will reject the assembly.
    /// </summary>
    public Guid ID { get; private init; } = new("db81e3b3-67de-48d6-acd0-7da499cb30c5");

    /// <summary>
    /// Display name shown in the Shoko Web UI plugin manager and browse page.
    /// </summary>
    public string Name { get; private init; } = "Upload File";

    /// <summary>
    /// Brief description of the plugin's purpose, shown in the plugin manager
    /// alongside the name and thumbnail.
    /// </summary>
    public string Description { get; private init; } =
        "Plugin for uploading files to named staging areas on the server.";

    /// <summary>
    /// Registers plugin services into the host DI container.
    /// Called once by Shoko's plugin manager during application startup.
    /// </summary>
    /// <param name="serviceCollection">The DI service collection to add services to.</param>
    /// <param name="applicationPaths">Host-provided application paths for config and data directories.</param>
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddSingleton<UploadService>();
    }
}
