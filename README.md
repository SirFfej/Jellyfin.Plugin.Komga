# Jellyfin Plugin for Komga

***ALPHA software — use at your own risk. Back up your Jellyfin data before enabling.***

Integrates Jellyfin with a self-hosted [Komga](https://komga.org) server, allowing you to browse your comics/manga/books in Jellyfin while leveraging Komga's superior reader experience. Provides metadata enrichment and bidirectional reading progress sync.

## Features

- **Metadata Provider**: Enriches locally-scanned comic book files with titles, summaries, genres, tags, authors, and cover images from Komga
- **Image Provider**: Fetches series cover thumbnails from your Komga server
- **Scheduled Tasks**:
  - Sync Komga Metadata (daily at 02:00)
  - Sync Reading Progress (every 4 hours)
- **Reader Integration**: Click "Open" on a comic to launch the Komga reader (requires [File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation))

## Requirements

- Jellyfin 10.10.x
- Komga server (any recent version)
- [.NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)

## Installation

### Option 1: Pre-built Release (Recommended)

1. Download the latest release ZIP from the [Releases](https://github.com/SirFfej/Jellyfin.Plugin.Komga/releases) page
2. Extract `Jellyfin.Plugin.Komga.dll` to your Jellyfin plugin folder:
   - Windows: `%programdata%\jellyfin\plugins\Komga`
   - Linux/Docker: `/var/lib/jellyfin/plugins/Komga`
3. Restart Jellyfin

### Option 2: Build from Source

```bash
git clone https://github.com/SirFfej/Jellyfin.Plugin.Komga.git
cd Jellyfin.Plugin.Komga/Jellyfin.Plugin.Komga
dotnet build -c Release
# Copy the DLL from bin/Release/net9.0/ to your Jellyfin plugins folder
```

## Configuration

1. After restarting Jellyfin, go to **Dashboard → Plugins → Komga**
2. Enter your Komga server details:
   - **Server URL**: Your Komga base URL (e.g., `http://192.168.1.10:25600` or `https://komga.example.com`)
   - **Username**: Your Komga email address
   - **Password** or **API Key**: Your password or an API key (recommended)
3. Optional settings:
   - **External / Browser URL**: Set this if Jellyfin and Komga use different URLs (e.g., Docker)
   - **Enable Metadata Provider**: Check to enrich local items with Komga metadata
   - **Enable Reading Progress Sync**: Check to sync read progress from Komga
4. Click **Test Connection** to verify, then **Save**

## Linking Comics to Komga

The plugin links Jellyfin items to Komga by matching folder/file names:

1. Ensure your Komga library has the same series/folder structure as your Jellyfin library
2. Run the **Sync Komga Metadata** task to discover and link items
3. Alternatively, browse to a comic in Jellyfin and the metadata provider will search Komga on refresh

## Usage

### Reading Progress Sync

When enabled, the plugin imports reading progress from Komga every 4 hours. Progress is one-way (Komga → Jellyfin only).

### Reader Redirect

To open comics in Komga's reader:

1. Install the [File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)
2. Configure both plugins
3. Click "Open" on any linked comic to launch the Komga reader

## Troubleshooting

- **Connection test fails**: Verify your Komga URL is reachable from Jellyfin's server
- **Metadata not linking**: Ensure folder names match exactly between Jellyfin and Komga
- **No cover images**: Run the metadata sync task or trigger a manual refresh

## License

MIT License — see LICENSE file for details.

## Support

For issues and feature requests, please use the [GitHub issue tracker](https://github.com/SirFfej/Jellyfin.Plugin.Komga/issues).