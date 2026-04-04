using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Komga.Api;
using Jellyfin.Plugin.Komga.Api.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Tasks;

/// <summary>
/// Scheduled task that updates Jellyfin <see cref="Book"/> items with metadata from Komga.
/// </summary>
/// <remarks>
/// For each <see cref="Book"/> item in the library the task resolves the matching Komga series
/// (by existing <c>ProviderIds["Komga"]</c> or by folder-name search), fetches metadata, and
/// writes it back to the Jellyfin item via <see cref="ILibraryManager.UpdateItemAsync"/>.
/// </remarks>
public class SyncKomgaMetadataTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly KomgaApiClientFactory _clientFactory;
    private readonly ILogger<SyncKomgaMetadataTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncKomgaMetadataTask"/> class.
    /// </summary>
    public SyncKomgaMetadataTask(
        ILibraryManager libraryManager,
        KomgaApiClientFactory clientFactory,
        ILogger<SyncKomgaMetadataTask> logger)
    {
        _libraryManager = libraryManager;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Komga Metadata";

    /// <inheritdoc />
    public string Key => "KomgaSyncMetadata";

    /// <inheritdoc />
    public string Description => "Fetches titles, summaries, genres, and covers from Komga for all matched Book items.";

    /// <inheritdoc />
    public string Category => "Komga";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null || !cfg.EnableMetadataProvider)
        {
            _logger.LogInformation("Komga metadata sync is disabled — skipping.");
            return;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Book],
            Recursive = true
        });

        if (items.Count == 0)
        {
            _logger.LogInformation("No Book items found in library.");
            return;
        }

        _logger.LogInformation("Starting Komga metadata sync for {Count} items.", items.Count);

        var client = _clientFactory.GetClient();

        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(100.0 * i / items.Count);

            var item = items[i];

            try
            {
                var series = await ResolveSeries(client, item, cancellationToken).ConfigureAwait(false);
                if (series is null)
                {
                    continue;
                }

                ApplyMetadata(item, series);

                await _libraryManager.UpdateItemAsync(
                    item,
                    item.GetParent(),
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Metadata sync failed for {Name}", item.Name);
            }
        }

        progress.Report(100);
        _logger.LogInformation("Komga metadata sync complete.");
    }

    private async Task<KomgaSeries?> ResolveSeries(KomgaApiClient client, BaseItem item, CancellationToken ct)
    {
        // Use existing series ID if already matched.
        if (item.ProviderIds.TryGetValue("Komga", out var seriesId) && !string.IsNullOrEmpty(seriesId))
        {
            return await client.GetSeriesAsync(seriesId, ct).ConfigureAwait(false);
        }

        // Search by folder name (series folder = parent of the book file).
        var searchName = GetSeriesFolderName(item.Path) ?? item.Name;
        if (string.IsNullOrWhiteSpace(searchName))
        {
            return null;
        }

        var page = await client.SearchSeriesAsync(searchName, ct).ConfigureAwait(false);
        if (page is null || page.Content.Count == 0)
        {
            return null;
        }

        return page.Content.FirstOrDefault(
                   s => string.Equals(s.Metadata.Title, searchName, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(s.Name, searchName, StringComparison.OrdinalIgnoreCase))
               ?? page.Content[0];
    }

    private static string? GetSeriesFolderName(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(dir) ? null : Path.GetFileName(dir);
    }

    private static void ApplyMetadata(BaseItem item, KomgaSeries series)
    {
        var meta = series.Metadata;
        var booksMeta = series.BooksMetadata;

        item.ProviderIds["Komga"] = series.Id;

        if (!string.IsNullOrWhiteSpace(meta.Title))
        {
            item.Name = meta.Title;
        }

        if (!string.IsNullOrWhiteSpace(meta.Summary))
        {
            item.Overview = meta.Summary;
        }

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
    }

    private static string? MapAgeRating(int ageRating) => ageRating switch
    {
        6  => "TV-Y7",
        9  => "PG",
        12 => "PG-13",
        15 => "TV-14",
        17 => "TV-MA",
        18 => "NC-17",
        _  => null
    };
}
