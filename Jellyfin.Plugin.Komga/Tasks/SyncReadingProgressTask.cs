using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Komga.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Tasks;

/// <summary>
/// Scheduled task that synchronises reading progress from Komga into Jellyfin user data.
/// </summary>
/// <remarks>
/// For every <see cref="Book"/> item that has a <c>ProviderIds["KomgaBook"]</c> the task
/// calls <c>GET /api/v1/books/{id}</c> and maps the embedded <c>readProgress</c>
/// to Jellyfin's <see cref="UserItemData.PlaybackPositionTicks"/> /
/// <see cref="UserItemData.Played"/> for all Jellyfin users.
/// </remarks>
public class SyncReadingProgressTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly KomgaApiClientFactory _clientFactory;
    private readonly ILogger<SyncReadingProgressTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncReadingProgressTask"/> class.
    /// </summary>
    public SyncReadingProgressTask(
        ILibraryManager libraryManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        KomgaApiClientFactory clientFactory,
        ILogger<SyncReadingProgressTask> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Komga Reading Progress";

    /// <inheritdoc />
    public string Key => "KomgaSyncReadingProgress";

    /// <inheritdoc />
    public string Description => "Imports reading progress (current page, completed status) from Komga into Jellyfin.";

    /// <inheritdoc />
    public string Category => "Komga";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerInterval,
            IntervalTicks = TimeSpan.FromHours(4).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null || !cfg.EnableReadingProgressSync)
        {
            _logger.LogInformation("Komga reading-progress sync is disabled — skipping.");
            return;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Book],
            Recursive = true
        });

        var matched = items
            .Where(i => i.ProviderIds.ContainsKey("KomgaBook"))
            .ToList();

        if (matched.Count == 0)
        {
            _logger.LogInformation("No Book items with a KomgaBook provider ID found.");
            return;
        }

        var users = _userManager.Users.ToList();
        _logger.LogInformation(
            "Syncing Komga reading progress for {Items} items across {Users} users.",
            matched.Count, users.Count);

        var client = _clientFactory.GetClient();

        for (int i = 0; i < matched.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(100.0 * i / matched.Count);

            var item = matched[i];
            if (!item.ProviderIds.TryGetValue("KomgaBook", out var bookId))
            {
                continue;
            }

            try
            {
                var book = await client.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
                if (book is null)
                {
                    continue;
                }

                var rp = book.ReadProgress;
                if (rp is null)
                {
                    continue;
                }

                int totalPages = book.Media.PagesCount;

                // Store fractional progress as PlaybackPositionTicks (0 = start, TotalTicks = end).
                // We use a conventional "total" of 10 000 ticks so the percentage is readable.
                const long TotalTicks = 10_000L;
                long positionTicks = totalPages > 0 && !rp.Completed
                    ? (long)Math.Clamp(TotalTicks * rp.Page / totalPages, 0, TotalTicks)
                    : rp.Completed ? TotalTicks : 0L;

                foreach (var user in users)
                {
                    var userData = _userDataManager.GetUserData(user, item);
                    userData.Played = rp.Completed;
                    userData.PlaybackPositionTicks = positionTicks;

                    _userDataManager.SaveUserData(
                        user,
                        item,
                        userData,
                        UserDataSaveReason.Import,
                        cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Progress sync failed for {Name} (book {BookId})", item.Name, bookId);
            }
        }

        progress.Report(100);
        _logger.LogInformation("Komga reading-progress sync complete.");
    }
}
