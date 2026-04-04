using System.Collections.Generic;

namespace Jellyfin.Plugin.Komga.Api.Models;

/// <summary>
/// Represents a single book (issue/chapter/volume) in Komga.
/// Returned by <c>GET /api/v1/books</c> and <c>GET /api/v1/series/{id}/books</c>.
/// </summary>
public class KomgaBook
{
    /// <summary>Gets or sets the book ID (UUID string).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning series ID.</summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning library ID.</summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the book display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the book number within its series.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the media (format/page count) details.</summary>
    public KomgaBookMedia Media { get; set; } = new();

    /// <summary>Gets or sets the book-level metadata.</summary>
    public KomgaBookMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the authenticated user's reading progress for this book.
    /// Null if the user has not started reading.
    /// </summary>
    public KomgaReadProgress? ReadProgress { get; set; }
}

/// <summary>Media information for a Komga book.</summary>
public class KomgaBookMedia
{
    /// <summary>Gets or sets the total page count.</summary>
    public int PagesCount { get; set; }

    /// <summary>Gets or sets the MIME media type (e.g. "application/zip").</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the processing status (e.g. "READY", "ERROR").</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Book-level metadata from Komga.</summary>
public class KomgaBookMetadata
{
    /// <summary>Gets or sets the book title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the book summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the issue/chapter number string (may differ from <see cref="KomgaBook.Number"/>).</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the release date (ISO 8601, e.g. "2023-06-15").</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>Gets or sets the book authors.</summary>
    public IReadOnlyList<KomgaAuthor> Authors { get; set; } = [];
}

/// <summary>Reading progress for a single book, as recorded by Komga.</summary>
public class KomgaReadProgress
{
    /// <summary>Gets or sets the last page read (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets a value indicating whether the book is marked as completed.</summary>
    public bool Completed { get; set; }
}
