using System.Collections.Generic;

namespace Jellyfin.Plugin.Komga.Api.Models;

/// <summary>
/// Represents a Komga series. Returned by <c>GET /api/v1/series</c>.
/// </summary>
public class KomgaSeries
{
    /// <summary>Gets or sets the series ID (UUID string).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning library ID.</summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the series folder name (not full path for non-admin users).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the series URL (full path for admins, filename only for others).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets total book count.</summary>
    public int BooksCount { get; set; }

    /// <summary>Gets or sets count of books the authenticated user has fully read.</summary>
    public int BooksReadCount { get; set; }

    /// <summary>Gets or sets count of books the authenticated user has not started.</summary>
    public int BooksUnreadCount { get; set; }

    /// <summary>Gets or sets count of books the authenticated user has started but not finished.</summary>
    public int BooksInProgressCount { get; set; }

    /// <summary>Gets or sets the series metadata (title, summary, genres, etc.).</summary>
    public KomgaSeriesMetadata Metadata { get; set; } = new();

    /// <summary>Gets or sets aggregated book metadata (authors, release date, etc.).</summary>
    public KomgaBookMetadataAggregation BooksMetadata { get; set; } = new();
}

/// <summary>Series-level metadata from Komga.</summary>
public class KomgaSeriesMetadata
{
    /// <summary>Gets or sets the display title (may differ from folder name).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the series summary / description.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the publisher name.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Gets or sets the age rating (null if unset).</summary>
    public int? AgeRating { get; set; }

    /// <summary>Gets or sets the BCP-47 language tag (e.g. "en", "ja").</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the genre set.</summary>
    public IReadOnlySet<string> Genres { get; set; } = new HashSet<string>();

    /// <summary>Gets or sets the tag set.</summary>
    public IReadOnlySet<string> Tags { get; set; } = new HashSet<string>();

    /// <summary>Gets or sets the publication status (e.g. "ONGOING", "ENDED").</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Aggregated book-level metadata (rolled up to series level) from Komga.</summary>
public class KomgaBookMetadataAggregation
{
    /// <summary>Gets or sets the list of authors across all books in the series.</summary>
    public IReadOnlyList<KomgaAuthor> Authors { get; set; } = [];

    /// <summary>Gets or sets the earliest book release date in the series.</summary>
    public string? ReleaseDate { get; set; }
}

/// <summary>An author entry from Komga metadata.</summary>
public class KomgaAuthor
{
    /// <summary>Gets or sets the author's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the author's role (e.g. "writer", "penciller", "colorist").</summary>
    public string Role { get; set; } = string.Empty;
}
