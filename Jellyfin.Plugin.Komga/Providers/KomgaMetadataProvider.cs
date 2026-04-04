using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Komga.Api;
using Jellyfin.Plugin.Komga.Api.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Providers;

/// <summary>
/// Fetches metadata for Jellyfin <see cref="Book"/> items from the Komga server.
/// </summary>
/// <remarks>
/// Matching strategy:
/// 1. If <c>ProviderIds["Komga"]</c> is already set, fetch the series directly.
/// 2. Otherwise walk the item path upward to find the series folder name,
///    then call <c>SearchSeriesAsync</c> and pick the best match (exact name first,
///    then first result).
/// </remarks>
public class KomgaMetadataProvider : IRemoteMetadataProvider<Book, BookInfo>
{
    private readonly KomgaApiClientFactory _clientFactory;
    private readonly ILogger<KomgaMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KomgaMetadataProvider"/> class.
    /// </summary>
    public KomgaMetadataProvider(KomgaApiClientFactory clientFactory, ILogger<KomgaMetadataProvider> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Komga";

    /// <inheritdoc />
    public async Task<MetadataResult<Book>> GetMetadata(BookInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Book>();

        if (!IsConfigured())
        {
            return result;
        }

        try
        {
            var client = _clientFactory.GetClient();
            var series = await ResolveSeries(client, info, cancellationToken).ConfigureAwait(false);

            if (series is null)
            {
                _logger.LogDebug("No Komga series found for {Name}", info.Name);
                return result;
            }

            result.HasMetadata = true;
            result.Item = new Book();
            result.Item.ProviderIds["Komga"] = series.Id;

            ApplyMetadata(result, series);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Komga metadata for {Name}", info.Name);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BookInfo searchInfo, CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return [];
        }

        try
        {
            var client = _clientFactory.GetClient();
            var query = searchInfo.Name;
            var page = await client.SearchSeriesAsync(query, cancellationToken).ConfigureAwait(false);

            return page?.Content.Select(s => new RemoteSearchResult
            {
                Name = s.Metadata.Title.Length > 0 ? s.Metadata.Title : s.Name,
                ProviderIds = new Dictionary<string, string> { ["Komga"] = s.Id }
            }) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Komga search failed for {Name}", searchInfo.Name);
            return [];
        }
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => throw new NotSupportedException("Image fetching is handled by KomgaImageProvider.");

    // -------------------------------------------------------------------------

    private static bool IsConfigured()
    {
        var cfg = Plugin.Instance?.Configuration;
        return cfg is not null
            && !string.IsNullOrWhiteSpace(cfg.KomgaServerUrl)
            && !string.IsNullOrWhiteSpace(cfg.Username)
            && !string.IsNullOrWhiteSpace(cfg.Password);
    }

    private async Task<KomgaSeries?> ResolveSeries(
        KomgaApiClient client,
        BookInfo info,
        CancellationToken ct)
    {
        // If we already have a Komga series ID, fetch it directly.
        if (info.ProviderIds.TryGetValue("Komga", out var existingId)
            && !string.IsNullOrEmpty(existingId))
        {
            return await client.GetSeriesAsync(existingId, ct).ConfigureAwait(false);
        }

        // Fall back to name search using the item name or parent folder name.
        var searchName = GetSeriesFolderName(info.Path) ?? info.Name;
        if (string.IsNullOrWhiteSpace(searchName))
        {
            return null;
        }

        var page = await client.SearchSeriesAsync(searchName, ct).ConfigureAwait(false);
        if (page is null || page.Content.Count == 0)
        {
            return null;
        }

        // Prefer an exact title match; fall back to the first result.
        return page.Content.FirstOrDefault(
                   s => string.Equals(s.Metadata.Title, searchName, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(s.Name, searchName, StringComparison.OrdinalIgnoreCase))
               ?? page.Content[0];
    }

    /// <summary>
    /// Walks the path upward from the book file to find the series folder name.
    /// For a file like <c>/media/Comics/My Series/Vol01.cbz</c> this returns <c>My Series</c>.
    /// </summary>
    private static string? GetSeriesFolderName(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // The book file is inside the series folder.
        var dir = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(dir) ? null : Path.GetFileName(dir);
    }

    private void ApplyMetadata(MetadataResult<Book> result, KomgaSeries series)
    {
        var item = result.Item;
        var meta = series.Metadata;
        var booksMeta = series.BooksMetadata;

        item.Name = meta.Title.Length > 0 ? meta.Title : series.Name;
        item.Overview = meta.Summary;

        if (!string.IsNullOrWhiteSpace(meta.Publisher))
        {
            item.Studios = [meta.Publisher];
        }

        if (meta.Genres?.Count > 0)
        {
            item.Genres = [.. meta.Genres];
        }

        if (meta.Tags?.Count > 0)
        {
            item.Tags = [.. meta.Tags];
        }

        if (!string.IsNullOrWhiteSpace(meta.Language))
        {
            item.PreferredMetadataLanguage = meta.Language;
        }

        if (!string.IsNullOrWhiteSpace(booksMeta?.ReleaseDate)
            && DateTime.TryParse(booksMeta.ReleaseDate, out var releaseDate))
        {
            item.ProductionYear = releaseDate.Year;
        }

        if (meta.AgeRating.HasValue)
        {
            item.OfficialRating = MapAgeRating(meta.AgeRating.Value);
        }

        // Authors
        if (booksMeta?.Authors is { Count: > 0 })
        {
            foreach (var author in booksMeta.Authors)
            {
                result.AddPerson(new PersonInfo
                {
                    Name = author.Name,
                    Type = MapAuthorRole(author.Role)
                });
            }
        }
    }

    private static string? MapAgeRating(int ageRating) => ageRating switch
    {
        0  => null,            // Unknown
        6  => "TV-Y7",
        9  => "PG",
        12 => "PG-13",
        15 => "TV-14",
        17 => "TV-MA",
        18 => "NC-17",
        _  => null
    };

    private static PersonKind MapAuthorRole(string role) => role.ToLowerInvariant() switch
    {
        "writer"      => PersonKind.Writer,
        "penciller"   => PersonKind.Penciller,
        "inker"       => PersonKind.Inker,
        "colorist"    => PersonKind.Colorist,
        "letterer"    => PersonKind.Letterer,
        "cover"       => PersonKind.CoverArtist,
        "editor"      => PersonKind.Editor,
        "translator"  => PersonKind.Translator,
        _             => PersonKind.Unknown
    };
}
