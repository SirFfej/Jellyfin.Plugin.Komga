using System;
using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Api;

/// <summary>
/// Creates and caches <see cref="KomgaApiClient"/> instances keyed by (baseUrl, username, password).
/// </summary>
public class KomgaApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KomgaApiClient> _clientLogger;
    private readonly ConcurrentDictionary<string, KomgaApiClient> _clients = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KomgaApiClientFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating named HTTP clients.</param>
    /// <param name="clientLogger">Logger passed to each created client.</param>
    public KomgaApiClientFactory(
        IHttpClientFactory httpClientFactory,
        ILogger<KomgaApiClient> clientLogger)
    {
        _httpClientFactory = httpClientFactory;
        _clientLogger = clientLogger;
    }

    /// <summary>
    /// Returns a client configured with the plugin's current server URL and credentials.
    /// </summary>
    public KomgaApiClient GetClient()
    {
        var config = Plugin.Instance?.Configuration;
        string baseUrl  = config?.NormalizedServerUrl ?? string.Empty;
        string username = config?.Username ?? string.Empty;
        string password = config?.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseUrl)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Komga server URL, username, and password must be configured before making API requests.");
        }

        // Cache key includes all three so changing credentials yields a new client.
        string cacheKey = $"{baseUrl}:{username}:{password}";

        return _clients.GetOrAdd(cacheKey, _ =>
        {
            var httpClient = _httpClientFactory.CreateClient(KomgaApiClient.HttpClientName);
            return new KomgaApiClient(httpClient, _clientLogger, baseUrl, username, password);
        });
    }

    /// <summary>
    /// Clears all cached client instances (e.g. after a configuration change).
    /// </summary>
    public void InvalidateAll() => _clients.Clear();
}
