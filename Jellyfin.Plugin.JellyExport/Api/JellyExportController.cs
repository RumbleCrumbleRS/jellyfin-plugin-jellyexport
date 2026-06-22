using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyExport.Configuration;
using Jellyfin.Plugin.JellyExport.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Common.Updates;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using MediaBrowser.Model.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyExport.Api;

/// <summary>
/// API controller exposing export / import of plugins, plugin settings,
/// repositories and branding. Administrator privileges are required.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("JellyExport")]
[Produces(MediaTypeNames.Application.Json)]
public class JellyExportController : ControllerBase
{
    private const string BrandingConfigKey = "branding";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IPluginManager _pluginManager;
    private readonly IInstallationManager _installationManager;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly IServerApplicationHost _applicationHost;
    private readonly ILogger<JellyExportController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyExportController"/> class.
    /// </summary>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="installationManager">The installation manager.</param>
    /// <param name="configurationManager">The server configuration manager.</param>
    /// <param name="applicationHost">The server application host.</param>
    /// <param name="logger">The logger.</param>
    public JellyExportController(
        IPluginManager pluginManager,
        IInstallationManager installationManager,
        IServerConfigurationManager configurationManager,
        IServerApplicationHost applicationHost,
        ILogger<JellyExportController> logger)
    {
        _pluginManager = pluginManager;
        _installationManager = installationManager;
        _configurationManager = configurationManager;
        _applicationHost = applicationHost;
        _logger = logger;
    }

    /// <summary>
    /// Builds and returns the export bundle as a JSON object.
    /// </summary>
    /// <returns>The export bundle.</returns>
    [HttpGet("Export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ExportBundle> Export()
    {
        return BuildBundle();
    }

    /// <summary>
    /// Builds the export bundle and returns it as a downloadable JSON file.
    /// </summary>
    /// <returns>A JSON file attachment.</returns>
    [HttpGet("Export/Download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult ExportDownload()
    {
        var bundle = BuildBundle();
        var json = JsonSerializer.SerializeToUtf8Bytes(bundle, _jsonOptions);
        var stamp = bundle.GeneratedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return File(json, MediaTypeNames.Application.Json, $"jellyexport-{stamp}.json");
    }

    /// <summary>
    /// Imports a previously exported bundle onto this server.
    /// </summary>
    /// <param name="request">The import request containing the bundle and options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import result.</returns>
    [HttpPost("Import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> Import(
        [FromBody] ImportRequest request,
        CancellationToken cancellationToken)
    {
        var bundle = request?.Bundle;
        if (bundle is null)
        {
            return BadRequest("Request body must contain a 'bundle'.");
        }

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var result = new ImportResult();

        var restoreRepositories = request!.RestoreRepositories ?? config.RestoreRepositories;
        var reinstallPlugins = request.ReinstallPlugins ?? config.ReinstallPlugins;
        var restoreBranding = request.RestoreBranding ?? config.IncludeBranding;

        if (request.RestoreSettings)
        {
            RestorePluginSettings(bundle, request.Overwrite, result);
        }
        else
        {
            result.Skipped.Add("Plugin settings restore disabled by request.");
        }

        if (restoreRepositories)
        {
            RestoreRepositories(bundle, result);
        }

        if (restoreBranding)
        {
            RestoreBranding(bundle, result);
        }

        if (reinstallPlugins)
        {
            await ReinstallPlugins(bundle, result, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result.Skipped.Add("Plugin re-installation disabled by request.");
        }

        result.RestartRecommended = result.Applied.Count > 0;
        result.Success = result.Errors.Count == 0;
        return result;
    }

    private ExportBundle BuildBundle()
    {
        var bundle = new ExportBundle
        {
            GeneratedUtc = DateTime.UtcNow,
            ServerVersion = _applicationHost.ApplicationVersionString
        };

        // Installed plugins.
        foreach (var plugin in _pluginManager.Plugins)
        {
            bundle.Plugins.Add(new ExportedPlugin
            {
                Id = plugin.Id,
                Name = plugin.Name,
                Version = plugin.Version?.ToString(),
                Status = plugin.Manifest?.Status.ToString()
            });
        }

        // Plugin configuration files (raw XML).
        var configDir = _configurationManager.ApplicationPaths.PluginConfigurationsPath;
        if (Directory.Exists(configDir))
        {
            foreach (var file in Directory.EnumerateFiles(configDir, "*.xml", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    bundle.PluginConfigurations.Add(new ExportedPluginConfig
                    {
                        FileName = Path.GetFileName(file),
                        Content = System.IO.File.ReadAllText(file)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "JellyExport: failed to read plugin configuration {File}", file);
                }
            }
        }

        // Plugin repositories.
        foreach (var repo in _configurationManager.Configuration.PluginRepositories ?? Array.Empty<RepositoryInfo>())
        {
            bundle.Repositories.Add(new ExportedRepository
            {
                Name = repo.Name,
                Url = repo.Url,
                Enabled = repo.Enabled
            });
        }

        // Branding / custom CSS.
        try
        {
            var branding = _configurationManager.GetConfiguration<BrandingOptions>(BrandingConfigKey);
            if (branding is not null)
            {
                bundle.Branding = new ExportedBranding
                {
                    CustomCss = branding.CustomCss,
                    LoginDisclaimer = branding.LoginDisclaimer,
                    SplashscreenEnabled = branding.SplashscreenEnabled
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JellyExport: failed to read branding options");
        }

        return bundle;
    }

    private void RestorePluginSettings(ExportBundle bundle, bool overwrite, ImportResult result)
    {
        var configDir = _configurationManager.ApplicationPaths.PluginConfigurationsPath;
        try
        {
            Directory.CreateDirectory(configDir);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Could not create plugin configuration directory: {ex.Message}");
            return;
        }

        foreach (var cfg in bundle.PluginConfigurations)
        {
            if (string.IsNullOrWhiteSpace(cfg.FileName))
            {
                continue;
            }

            // Guard against path traversal: only ever write a bare file name.
            var safeName = Path.GetFileName(cfg.FileName);
            var target = Path.Combine(configDir, safeName);

            if (System.IO.File.Exists(target) && !overwrite)
            {
                result.Skipped.Add($"Setting {safeName} already exists (overwrite disabled).");
                continue;
            }

            try
            {
                System.IO.File.WriteAllText(target, cfg.Content);
                result.Applied.Add($"Restored plugin settings: {safeName}");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to write {safeName}: {ex.Message}");
            }
        }
    }

    private void RestoreRepositories(ExportBundle bundle, ImportResult result)
    {
        if (bundle.Repositories.Count == 0)
        {
            return;
        }

        try
        {
            var existing = (_configurationManager.Configuration.PluginRepositories ?? Array.Empty<RepositoryInfo>()).ToList();
            var known = new HashSet<string>(
                existing.Where(r => r.Url is not null).Select(r => r.Url!),
                StringComparer.OrdinalIgnoreCase);

            var added = 0;
            foreach (var repo in bundle.Repositories)
            {
                if (string.IsNullOrWhiteSpace(repo.Url) || !known.Add(repo.Url!))
                {
                    continue;
                }

                existing.Add(new RepositoryInfo
                {
                    Name = repo.Name,
                    Url = repo.Url,
                    Enabled = repo.Enabled
                });
                added++;
            }

            if (added > 0)
            {
                _configurationManager.Configuration.PluginRepositories = existing.ToArray();
                _configurationManager.SaveConfiguration();
                result.Applied.Add($"Restored {added} plugin repository(ies).");
            }
            else
            {
                result.Skipped.Add("All plugin repositories already present.");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to restore repositories: {ex.Message}");
        }
    }

    private void RestoreBranding(ExportBundle bundle, ImportResult result)
    {
        if (bundle.Branding is null)
        {
            return;
        }

        try
        {
            var branding = _configurationManager.GetConfiguration<BrandingOptions>(BrandingConfigKey) ?? new BrandingOptions();
            branding.CustomCss = bundle.Branding.CustomCss;
            branding.LoginDisclaimer = bundle.Branding.LoginDisclaimer;
            branding.SplashscreenEnabled = bundle.Branding.SplashscreenEnabled;
            _configurationManager.SaveConfiguration(BrandingConfigKey, branding);
            result.Applied.Add("Restored branding / custom CSS.");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to restore branding: {ex.Message}");
        }
    }

    private async Task ReinstallPlugins(ExportBundle bundle, ImportResult result, CancellationToken cancellationToken)
    {
        if (bundle.Plugins.Count == 0)
        {
            return;
        }

        var installed = new HashSet<Guid>(_pluginManager.Plugins.Select(p => p.Id));

        IReadOnlyList<PackageInfo> packages;
        try
        {
            packages = await _installationManager.GetAvailablePackages(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Could not load plugin catalog from repositories: {ex.Message}");
            return;
        }

        foreach (var plugin in bundle.Plugins)
        {
            if (plugin.Id != Guid.Empty && installed.Contains(plugin.Id))
            {
                result.Skipped.Add($"Plugin already installed: {plugin.Name}");
                continue;
            }

            Version? specificVersion = null;
            if (!string.IsNullOrWhiteSpace(plugin.Version)
                && Version.TryParse(plugin.Version, out var parsed))
            {
                specificVersion = parsed;
            }

            try
            {
                // Prefer an exact version match, fall back to latest compatible.
                var candidate = _installationManager
                    .GetCompatibleVersions(packages, plugin.Name, plugin.Id, specificVersion: specificVersion)
                    .FirstOrDefault()
                    ?? _installationManager
                        .GetCompatibleVersions(packages, plugin.Name, plugin.Id)
                        .FirstOrDefault();

                if (candidate is null)
                {
                    result.Skipped.Add($"No catalog match for plugin: {plugin.Name} (add its repository and retry).");
                    continue;
                }

                await _installationManager.InstallPackage(candidate, cancellationToken).ConfigureAwait(false);
                result.Applied.Add($"Queued install: {plugin.Name} {candidate.Version}");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to install {plugin.Name}: {ex.Message}");
            }
        }
    }
}
