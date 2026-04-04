using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Komga;

/// <summary>
/// Plugin configuration persisted as XML by <see cref="MediaBrowser.Common.Plugins.BasePlugin{T}"/>.
/// </summary>
/// <remarks>
/// Do NOT add JSON attributes to this class — Jellyfin serialises it with its internal
/// <c>IXmlSerializer</c>, not <c>System.Text.Json</c>.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the Komga server base URL (no trailing slash).
    /// Example: <c>http://192.168.1.10:25600</c> or <c>https://komga.example.com</c>.
    /// </summary>
    public string KomgaServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Komga username (email address used to log in).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Komga password or API key.
    /// An API key can be created in Komga under Account → API Keys and used as the password
    /// with any username for HTTP Basic authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Komga URL as seen from the user's browser.
    /// May differ from <see cref="KomgaServerUrl"/> when Docker internal hostnames are used.
    /// Example: <c>http://192.168.1.10:25600</c>.
    /// </summary>
    public string KomgaExternalUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the metadata provider is active.
    /// </summary>
    public bool EnableMetadataProvider { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether reading progress sync is active.
    /// </summary>
    public bool EnableReadingProgressSync { get; set; } = true;

    /// <summary>
    /// Returns the server URL with no trailing slash, safe for URL construction.
    /// </summary>
    public string NormalizedServerUrl => KomgaServerUrl.TrimEnd('/');
}
