using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Komga.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Api;

/// <summary>
/// Thin HTTP wrapper around the Komga REST API.
/// One instance per (baseUrl, username, password) triple — obtain instances via
/// <see cref="KomgaApiClientFactory"/>.
/// </summary>
/// <remarks>
/// Komga uses HTTP Basic authentication. All requests include an
/// <c>Authorization: Basic {base64(username:password)}</c> header.
/// An API key created in Komga (Account → API Keys) can be used as the password
/// with any username.
/// </remarks>
public class KomgaApiClient
{
    /// <summary>Named client key registered with <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "KomgaClient";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly ILogger<KomgaApiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KomgaApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">Pre-configured named HTTP client.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="baseUrl">Komga server base URL (no trailing slash).</param>
    /// <param name="username">Komga username (email).</param>
    /// <param name="password">Komga password or API key.</param>
    public KomgaApiClient(
        HttpClient httpClient,
        ILogger<KomgaApiClient> logger,
        string baseUrl,
        string username,
        string password)
    {
        _http = httpClient;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        // Komga uses HTTP Basic auth on every request.
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Connection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests connectivity to the Komga server using the configured credentials.
    /// Returns <c>true</c> if the server is reachable and the credentials are valid.
    /// Komga exposes <c>GET /api/v1/libraries</c> which requires authentication —
    /// a 200 response means the server is up and credentials are accepted.
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http
                .GetAsync("api/v1/libraries", ct)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Komga connection test failed");
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Libraries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all libraries the authenticated user can access.
    /// </summary>
    public async Task<List<KomgaLibrary>> GetLibrariesAsync(CancellationToken ct = default)
    {
        var result = await GetAsync<List<KomgaLibrary>>("api/v1/libraries", ct).ConfigureAwait(false);
        return result ?? [];
    }

    // -------------------------------------------------------------------------
    // Series
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all series in the given library, fetching every page automatically.
    /// </summary>
    public async Task<List<KomgaSeries>> GetLibrarySeriesAsync(string libraryId, CancellationToken ct = default)
    {
        var all = new List<KomgaSeries>();
        int page = 0;

        while (true)
        {
            var pageResult = await GetAsync<KomgaPage<KomgaSeries>>(
                $"api/v1/series?library_id={Uri.EscapeDataString(libraryId)}&page={page}&size=100",
                ct).ConfigureAwait(false);

            if (pageResult is null || pageResult.Content.Count == 0)
            {
                break;
            }

            all.AddRange(pageResult.Content);

            if (page >= pageResult.TotalPages - 1)
            {
                break;
            }

            page++;
        }

        return all;
    }

    /// <summary>
    /// Returns a single series by ID.
    /// </summary>
    public Task<KomgaSeries?> GetSeriesAsync(string seriesId, CancellationToken ct = default)
        => GetAsync<KomgaSeries>($"api/v1/series/{Uri.EscapeDataString(seriesId)}", ct);

    /// <summary>
    /// Searches for series by name. Returns the first page of results (up to 20).
    /// </summary>
    public Task<KomgaPage<KomgaSeries>?> SearchSeriesAsync(string query, CancellationToken ct = default)
        => GetAsync<KomgaPage<KomgaSeries>>(
            $"api/v1/series?search={Uri.EscapeDataString(query)}&size=20",
            ct);

    /// <summary>
    /// Downloads the thumbnail image for a series. Returns raw bytes, or <c>null</c> on failure.
    /// </summary>
    public Task<byte[]?> GetSeriesThumbnailAsync(string seriesId, CancellationToken ct = default)
        => GetBytesAsync($"api/v1/series/{Uri.EscapeDataString(seriesId)}/thumbnail", ct);

    // -------------------------------------------------------------------------
    // Books
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns all books in the given series using Komga's <c>?unpaged=true</c> shortcut.
    /// </summary>
    public async Task<List<KomgaBook>> GetBooksAsync(string seriesId, CancellationToken ct = default)
    {
        var page = await GetAsync<KomgaPage<KomgaBook>>(
            $"api/v1/series/{Uri.EscapeDataString(seriesId)}/books?unpaged=true",
            ct).ConfigureAwait(false);

        return page?.Content is not null ? [.. page.Content] : [];
    }

    /// <summary>
    /// Returns a single book by ID (includes read-progress for the current user).
    /// </summary>
    public Task<KomgaBook?> GetBookAsync(string bookId, CancellationToken ct = default)
        => GetAsync<KomgaBook>($"api/v1/books/{Uri.EscapeDataString(bookId)}", ct);

    /// <summary>
    /// Downloads the thumbnail image for a book. Returns raw bytes, or <c>null</c> on failure.
    /// </summary>
    public Task<byte[]?> GetBookThumbnailAsync(string bookId, CancellationToken ct = default)
        => GetBytesAsync($"api/v1/books/{Uri.EscapeDataString(bookId)}/thumbnail", ct);

    // -------------------------------------------------------------------------
    // Reading progress
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates the reading progress for the current user on a book.
    /// Set <see cref="KomgaReadProgressUpdate.Completed"/> = <c>true</c> to mark as fully read.
    /// </summary>
    public Task<bool> MarkReadProgressAsync(
        string bookId,
        KomgaReadProgressUpdate update,
        CancellationToken ct = default)
        => PatchAsync($"api/v1/books/{Uri.EscapeDataString(bookId)}/read-progress", update, ct);

    /// <summary>
    /// Deletes all reading progress for the current user on a book.
    /// </summary>
    public Task<bool> DeleteReadProgressAsync(string bookId, CancellationToken ct = default)
        => DeleteAsync($"api/v1/books/{Uri.EscapeDataString(bookId)}/read-progress", ct);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct)
        where T : class
    {
        try
        {
            using var response = await _http.GetAsync(endpoint, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Komga API {Endpoint} returned {Status}", endpoint, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Komga API request failed: {Endpoint}", endpoint);
            return null;
        }
    }

    private async Task<byte[]?> GetBytesAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(endpoint, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Komga API {Endpoint} returned {Status}", endpoint, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Komga API request failed: {Endpoint}", endpoint);
            return null;
        }
    }

    private async Task<bool> PatchAsync<T>(string endpoint, T body, CancellationToken ct)
        where T : class
    {
        try
        {
            using var content = JsonContent.Create(body, options: JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Komga API PATCH {Endpoint} returned {Status}", endpoint, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Komga API PATCH request failed: {Endpoint}", endpoint);
            return false;
        }
    }

    private async Task<bool> DeleteAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            using var response = await _http.DeleteAsync(endpoint, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Komga API DELETE {Endpoint} returned {Status}", endpoint, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Komga API DELETE request failed: {Endpoint}", endpoint);
            return false;
        }
    }
}
