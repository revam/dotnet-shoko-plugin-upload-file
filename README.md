# Shoko Upload File Plugin

A [Shoko](https://shokoanime.com/) plugin that exposes API endpoints for uploading files to named staging areas on the server.

## Features

- **Named Areas** — Create named upload destinations referencing a managed folder and relative path.
- **Checksum Validation** — Accepts files with `sha1:`, `sha256:`, or `md5:` checksum headers; validates after upload.
- **Admin-only API** — All endpoints require an authenticated Shoko admin user.
- **Async Locking** — Concurrent uploads to the same path are serialized via `AsyncKeyedLock`.

## Installation

### GUI (Recommended)

1. Open the Shoko Web UI and navigate to **Settings → Plugins → Repositories**.
2. Add the manifest URL:
   ```
   https://raw.githubusercontent.com/revam/dotnet-shoko-plugin-upload-file/stable/manifest.json
   ```
3. Go to **Settings → Plugins → Browse** and find **Upload File**.
4. Click **Install** on the desired version.
5. Restart Shoko.

### Manual

1. Download the latest `Shoko.Plugin.UploadFile-<version>-any.zip` from the [Releases](../../releases) page.
2. Extract the ZIP and place `Shoko.Plugin.UploadFile.dll` into your Shoko **Plugins** folder.
3. Restart Shoko.

## Configuration

The plugin exposes the following settings in the Shoko UI:

| Setting | Default | Description |
|---|---|---|
| **Enabled** | `true` | Master switch to enable/disable the plugin. |
| **Use Subdirectories** | `false` | When enabled, files are placed in `{hash[0..2]}/{hash[2..4]}/{filename}` subdirectories instead of directly in the area root. |

## API Reference

Open `http://&lt;shoko hostname&gt;:8111/swagger` in your browser and select **Upload File V1** from the server dropdown in the top-right corner to explore and test all available endpoints.

## Building from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet restore
dotnet build --configuration Release
```

The compiled assembly will be located at `source/bin/Release/net10.0/Shoko.Plugin.UploadFile.dll`.

## License

This project is licensed under the MIT License.
