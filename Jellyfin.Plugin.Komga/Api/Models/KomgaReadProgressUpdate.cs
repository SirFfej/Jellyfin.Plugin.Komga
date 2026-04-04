namespace Jellyfin.Plugin.Komga.Api.Models;

/// <summary>
/// Request body for <c>PATCH /api/v1/books/{id}/read-progress</c>.
/// Either <see cref="Page"/> or <see cref="Completed"/> = true must be supplied.
/// </summary>
public class KomgaReadProgressUpdate
{
    /// <summary>Gets or sets the last page read (1-based). Required if not marking as completed.</summary>
    public int? Page { get; set; }

    /// <summary>Gets or sets a value indicating whether the book is fully read.</summary>
    public bool? Completed { get; set; }
}
