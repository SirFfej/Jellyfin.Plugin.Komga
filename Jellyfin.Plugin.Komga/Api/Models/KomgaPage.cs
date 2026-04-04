using System.Collections.Generic;

namespace Jellyfin.Plugin.Komga.Api.Models;

/// <summary>
/// A Spring-style paginated response wrapper returned by Komga list endpoints.
/// </summary>
/// <typeparam name="T">The DTO type of each item in the page.</typeparam>
public class KomgaPage<T>
{
    /// <summary>Gets or sets the items in this page.</summary>
    public IReadOnlyList<T> Content { get; set; } = [];

    /// <summary>Gets or sets the total number of pages.</summary>
    public int TotalPages { get; set; }

    /// <summary>Gets or sets the total number of elements across all pages.</summary>
    public long TotalElements { get; set; }

    /// <summary>Gets or sets the current page number (0-based).</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the page size.</summary>
    public int Size { get; set; }
}
