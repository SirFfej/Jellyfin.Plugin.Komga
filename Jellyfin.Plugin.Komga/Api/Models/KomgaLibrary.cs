namespace Jellyfin.Plugin.Komga.Api.Models;

/// <summary>
/// Represents a Komga library. Returned by <c>GET /api/v1/libraries</c>.
/// </summary>
public class KomgaLibrary
{
    /// <summary>Gets or sets the library ID (UUID string).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable library name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the root filesystem path (empty for non-admin users).</summary>
    public string Root { get; set; } = string.Empty;
}
