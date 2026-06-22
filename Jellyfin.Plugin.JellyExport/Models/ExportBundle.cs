using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyExport.Models;

/// <summary>
/// A portable bundle describing a server's installed plugins, plugin settings,
/// plugin repositories and branding. Produced by the export endpoint and consumed
/// by the import endpoint.
/// </summary>
public class ExportBundle
{
    /// <summary>
    /// Gets or sets the bundle format version. Incremented when the shape of the
    /// bundle changes in a breaking way.
    /// </summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the tool that produced the bundle.
    /// </summary>
    public string GeneratedBy { get; set; } = "JellyExport";

    /// <summary>
    /// Gets or sets the UTC timestamp the bundle was generated.
    /// </summary>
    public DateTime GeneratedUtc { get; set; }

    /// <summary>
    /// Gets or sets the version of the Jellyfin server the bundle was exported from.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Gets the installed plugins.
    /// </summary>
    public List<ExportedPlugin> Plugins { get; init; } = new();

    /// <summary>
    /// Gets the raw plugin configuration files.
    /// </summary>
    public List<ExportedPluginConfig> PluginConfigurations { get; init; } = new();

    /// <summary>
    /// Gets the configured plugin repositories.
    /// </summary>
    public List<ExportedRepository> Repositories { get; init; } = new();

    /// <summary>
    /// Gets or sets the branding / custom CSS options.
    /// </summary>
    public ExportedBranding? Branding { get; set; }
}

/// <summary>
/// Describes a single installed plugin.
/// </summary>
public class ExportedPlugin
{
    /// <summary>Gets or sets the plugin id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the installed plugin version.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the plugin status (e.g. Active, Disabled).</summary>
    public string? Status { get; set; }
}

/// <summary>
/// A single plugin configuration file captured as raw text.
/// </summary>
public class ExportedPluginConfig
{
    /// <summary>Gets or sets the configuration file name (e.g. "MyPlugin.xml").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw file contents.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// A plugin repository entry.
/// </summary>
public class ExportedRepository
{
    /// <summary>Gets or sets the repository display name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the repository manifest URL.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets a value indicating whether the repository is enabled.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Server branding / custom CSS.
/// </summary>
public class ExportedBranding
{
    /// <summary>Gets or sets the custom CSS injected into the web client.</summary>
    public string? CustomCss { get; set; }

    /// <summary>Gets or sets the login page disclaimer.</summary>
    public string? LoginDisclaimer { get; set; }

    /// <summary>Gets or sets a value indicating whether the splash screen is enabled.</summary>
    public bool SplashscreenEnabled { get; set; }
}
