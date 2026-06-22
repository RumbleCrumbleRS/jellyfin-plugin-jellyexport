# JellyExport

A Jellyfin server plugin that exports a server's **installed plugins, plugin
settings, plugin repositories and custom CSS / branding** into a single portable
file, then re-imports that bundle onto a fresh server to reproduce the same setup.

> Target: Jellyfin **10.11.x** (`net9.0`).

## What it captures

| Item | Export | Import |
| --- | --- | --- |
| Installed plugins (name, id, version) | ✅ listed | ✅ re-installed from repositories |
| Plugin settings (`plugins/configurations/*.xml`) | ✅ raw files | ✅ written back |
| Plugin repositories | ✅ | ✅ added if missing |
| Custom CSS / branding (`CustomCss`, login disclaimer, splash screen) | ✅ | ✅ restored |

The bundle is plain JSON (`FormatVersion: 1`) so it is easy to inspect, diff and
keep in source control.

## Endpoints

All endpoints require an **administrator** access token
(`Authorize: RequiresElevation`).

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/JellyExport/Export` | Return the export bundle as JSON. |
| `GET` | `/JellyExport/Export/Download` | Return the bundle as a downloadable `.json` file. |
| `POST` | `/JellyExport/Import` | Apply a bundle. Body: `{ "Bundle": { ... }, "Overwrite": true }`. |

`POST /JellyExport/Import` accepts an `ImportRequest`:

```jsonc
{
  "Bundle": { /* an exported bundle */ },
  "RestoreSettings": true,        // write plugin *.xml settings
  "RestoreRepositories": true,    // add missing plugin repositories (defaults to plugin config)
  "ReinstallPlugins": true,       // queue installs from repositories (defaults to plugin config)
  "RestoreBranding": true,        // restore custom CSS / branding (defaults to plugin config)
  "Overwrite": true               // overwrite existing settings files
}
```

The response (`ImportResult`) lists what was `Applied`, `Skipped` and any
`Errors`, plus `RestartRecommended`. **Restart the server** after import so newly
installed plugins and restored settings load.

## Usage from the dashboard

Install the plugin, open **Dashboard → Plugins → JellyExport**:

- **Download export bundle** saves `jellyexport.json`.
- **Import selected bundle** uploads a bundle and applies it.
- The checkboxes set the default import behaviour.

## Notes & limitations

- Re-installing plugins requires their **repositories** to be reachable. JellyExport
  restores the repositories from the bundle first; a plugin published only to a
  private repository that is not in the bundle cannot be auto-installed.
- Plugin settings files can contain secrets (API keys, tokens). Treat the export
  file as **sensitive** and transfer it over a trusted channel.
- Path-traversal is guarded: only bare file names are written into
  `plugins/configurations`.

## Build

```bash
dotnet build -c Release -f net9.0
```

The plugin DLL is produced at
`bin/Release/net9.0/Jellyfin.Plugin.JellyExport.dll`. Copy it into a
`plugins/JellyExport` folder under your Jellyfin data directory, or package it
with [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) using
`build.yaml`.
