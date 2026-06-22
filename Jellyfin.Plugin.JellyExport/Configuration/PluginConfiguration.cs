using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyExport.Configuration;

/// <summary>
/// Plugin configuration for JellyExport.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether plugin repositories should be
    /// restored (added back to the server) during import. Enabled by default so
    /// that the listed plugins can actually be re-installed on the target server.
    /// </summary>
    public bool RestoreRepositories { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the importer should attempt to
    /// re-install the plugins listed in the bundle from the configured
    /// repositories. Enabled by default.
    /// </summary>
    public bool ReinstallPlugins { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether custom CSS / branding options are
    /// included in the export and restored on import.
    /// </summary>
    public bool IncludeBranding { get; set; } = true;
}
