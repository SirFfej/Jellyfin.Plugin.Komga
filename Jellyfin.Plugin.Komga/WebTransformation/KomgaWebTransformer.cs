using System;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Komga.WebTransformation;

/// <summary>
/// Called by the File Transformation plugin via reflection when Jellyfin's
/// <c>index.html</c> is served to a browser.
/// Injects a small bridge script that detects Komga-linked items and redirects
/// "Open" clicks to the Komga reader.
/// </summary>
public static class KomgaWebTransformer
{
    /// <summary>
    /// Transforms the Jellyfin web UI HTML.
    /// </summary>
    /// <param name="payload">
    /// JSON object with a <c>"contents"</c> field containing the original HTML.
    /// </param>
    /// <returns>Modified HTML string.</returns>
    public static string Transform(JObject payload)
    {
        string html = payload["contents"]?.ToString() ?? string.Empty;

        const string Injection =
            "<script src=\"/Komga/bridge.js\" defer></script>";

        return html.Replace(
            "</body>",
            Injection + "</body>",
            StringComparison.OrdinalIgnoreCase);
    }
}
