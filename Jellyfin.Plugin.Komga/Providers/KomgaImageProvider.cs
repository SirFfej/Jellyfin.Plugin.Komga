using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Komga.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Providers;

/// <summary>
/// Provides primary cover images for Jellyfin <see cref="Book"/> items from Komga.
/// </summary>
/// <remarks>
/// The <c>ProviderIds["Komga"]</c> series ID is used to fetch the thumbnail via the Komga API.
/// Images are fetched at <see cref="GetImageResponse"/> time using a synthetic URL that encodes
/// the series ID, allowing Jellyfin to call back into this provider.
/// </remarks>
public class KomgaImageProvider : IRemoteImageProvider
{
    /// <summary>URL scheme prefix used to encode Komga series thumbnail requests.</summary>
    private const string ThumbnailScheme = "komga-thumbnail://series/";

    private readonly KomgaApiClientFactory _clientFactory;
    private readonly ILogger<KomgaImageProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KomgaImageProvider"/> class.
    /// </summary>
    public KomgaImageProvider(KomgaApiClientFactory clientFactory, ILogger<KomgaImageProvider> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Komga";

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Book;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        yield return ImageType.Primary;
    }

    /// <inheritdoc />
    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (!item.ProviderIds.TryGetValue("Komga", out var seriesId)
            || string.IsNullOrEmpty(seriesId))
        {
            return Task.FromResult<IEnumerable<RemoteImageInfo>>([]);
        }

        var imageInfo = new RemoteImageInfo
        {
            ProviderName = Name,
            Type = ImageType.Primary,
            Url = ThumbnailScheme + seriesId
        };

        return Task.FromResult<IEnumerable<RemoteImageInfo>>([imageInfo]);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        if (!url.StartsWith(ThumbnailScheme, StringComparison.Ordinal))
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
        }

        var seriesId = url[ThumbnailScheme.Length..];

        try
        {
            var client = _clientFactory.GetClient();
            var bytes = await client.GetSeriesThumbnailAsync(seriesId, cancellationToken).ConfigureAwait(false);

            if (bytes is null || bytes.Length == 0)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new ByteArrayContent(bytes);
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Komga thumbnail for series {SeriesId}", seriesId);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }
}
