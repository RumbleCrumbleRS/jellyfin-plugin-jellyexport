using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyExport.Models;

/// <summary>
/// Options controlling what an import applies. All flags default to the plugin
/// configuration when omitted by the caller.
/// </summary>
public class ImportRequest
{
    /// <summary>Gets or sets the bundle to import.</summary>
    public ExportBundle? Bundle { get; set; }

    /// <summary>Gets or sets a value indicating whether plugin settings files are restored.</summary>
    public bool RestoreSettings { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether plugin repositories are restored.</summary>
    public bool? RestoreRepositories { get; set; }

    /// <summary>Gets or sets a value indicating whether listed plugins are re-installed.</summary>
    public bool? ReinstallPlugins { get; set; }

    /// <summary>Gets or sets a value indicating whether branding / custom CSS is restored.</summary>
    public bool? RestoreBranding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether existing plugin configuration files
    /// are overwritten. When false, files that already exist are left untouched.
    /// </summary>
    public bool Overwrite { get; set; } = true;
}

/// <summary>
/// The outcome of an import operation.
/// </summary>
public class ImportResult
{
    /// <summary>Gets a value indicating whether the import completed without fatal errors.</summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a server restart is recommended for
    /// all changes (newly installed plugins, settings) to take effect.
    /// </summary>
    public bool RestartRecommended { get; set; }

    /// <summary>Gets the human-readable list of actions that were applied.</summary>
    public List<string> Applied { get; init; } = new();

    /// <summary>Gets the human-readable list of actions that were skipped.</summary>
    public List<string> Skipped { get; init; } = new();

    /// <summary>Gets the human-readable list of actions that failed.</summary>
    public List<string> Errors { get; init; } = new();
}
