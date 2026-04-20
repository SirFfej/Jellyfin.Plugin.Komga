using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Komga.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga.Controllers;

/// <summary>
/// Provides Komga plugin endpoints used by the config page and the browser-side bridge script.
/// </summary>
[ApiController]
[Route("[controller]")]
public class KomgaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<KomgaController> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="KomgaController"/> class.
    /// </summary>
    public KomgaController(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, ILibraryManager libraryManager)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<KomgaController>();
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Tests the supplied Komga URL and credentials without persisting them.
    /// Called from <c>configPage.html</c> with the values the user has just typed.
    /// </summary>
    /// <param name="serverUrl">Base URL of the Komga server (e.g. <c>http://komga:8080</c>).</param>
    /// <param name="username">Komga username or any string when using an API key as password.</param>
    /// <param name="password">Komga password or API key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TestConnectionResult"/> indicating success or the error message.</returns>
    [HttpGet("TestConnection")]
    [Authorize(Policy = "DefaultAuthorization")]
    public async Task<ActionResult<TestConnectionResult>> TestConnection(
        [FromQuery] string serverUrl,
        [FromHeader(Name = "X-Komga-Username")] string username,
        [FromHeader(Name = "X-Komga-Password")] string password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return BadRequest(new TestConnectionResult(false, "serverUrl is required."));
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new TestConnectionResult(false, "Username and password are required."));
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient(KomgaApiClient.HttpClientName);
            var clientLogger = _loggerFactory.CreateLogger<KomgaApiClient>();
            var client = new KomgaApiClient(httpClient, clientLogger, serverUrl, username, password);
            var ok = await client.TestConnectionAsync(ct).ConfigureAwait(false);
            return Ok(ok
                ? new TestConnectionResult(true)
                : new TestConnectionResult(false, "Server responded but credentials were rejected (HTTP 401/403)."));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TestConnection failed for {Url}", serverUrl);
            return Ok(new TestConnectionResult(false, ex.Message));
        }
    }

    /// <summary>
    /// Returns the Komga external URL used by the browser-side redirect script.
    /// Falls back to the server URL if no separate external URL is configured.
    /// </summary>
    [HttpGet("Config")]
    [Authorize(Policy = "DefaultAuthorization")]
    public ActionResult<KomgaConfigResult> GetConfig()
    {
        var cfg = Plugin.Instance?.Configuration;
        var url = !string.IsNullOrWhiteSpace(cfg?.KomgaExternalUrl)
            ? cfg.KomgaExternalUrl
            : cfg?.NormalizedServerUrl
            ?? string.Empty;

        return Ok(new KomgaConfigResult(url));
    }

    /// <summary>
    /// Serves the bridge JavaScript that is injected into the Jellyfin web UI by the
    /// File Transformation plugin. It overrides the play/open action on items that have
    /// a <c>Komga</c> provider ID, redirecting the browser to the Komga reader.
    /// </summary>
    [HttpGet("bridge.js")]
    [AllowAnonymous]
    public ContentResult GetBridgeJs()
    {
        const string Script = """
            (function () {
                'use strict';

                async function getKomgaUrl() {
                    try {
                        const resp = await fetch('/Komga/Config', {
                            headers: { 'Authorization': 'MediaBrowser Token="' + (ApiClient && ApiClient._token) + '"' }
                        });
                        if (!resp.ok) return null;
                        const data = await resp.json();
                        return data.ExternalUrl || null;
                    } catch { return null; }
                }

                function getItemId() {
                    const match = window.location.hash.match(/[?&]id=([^&]+)/);
                    return match ? match[1] : null;
                }

                async function tryRedirect() {
                    const itemId = getItemId();
                    if (!itemId) return;

                    try {
                        const item = await ApiClient.getItem(ApiClient.getCurrentUserId(), itemId);
                        const komgaId = item && item.ProviderIds && item.ProviderIds['Komga'];
                        if (!komgaId) return;

                        const komgaUrl = await getKomgaUrl();
                        if (!komgaUrl) return;

                        const seriesUrl = komgaUrl.replace(/\/$/, '') + '/series/' + encodeURIComponent(komgaId);

                        // Override the primary play/read button.
                        const buttons = document.querySelectorAll('[data-action="play"], .btnPlay, .itemAction[data-action]');
                        buttons.forEach(function (btn) {
                            btn.addEventListener('click', function (e) {
                                e.stopImmediatePropagation();
                                e.preventDefault();
                                window.open(seriesUrl, '_blank');
                            }, true);
                        });
                    } catch (err) {
                        console.debug('[Komga] bridge error:', err);
                    }
                }

                // Run on every hash-change (Jellyfin is a SPA).
                window.addEventListener('hashchange', function () { tryRedirect(); });
                tryRedirect();
            })();
            """;

        return Content(Script, "application/javascript");
    }

    /// <summary>
    /// Gets the Jellyfin libraries that can be linked with Komga.
    /// Returns book libraries.
    /// </summary>
    [HttpGet("Libraries")]
    //[Authorize(Policy = "Default")]  // Temp disabled for debug
    public async Task<ActionResult> GetLibraries()
    {
        try
        {
            var libraries = new List<LibraryDto>();

            await Task.Run(() =>
            {
                try
                {
                    var virtualFolders = _libraryManager.GetVirtualFolders();
                    if (virtualFolders != null)
                    {
                        foreach (var lf in virtualFolders)
                        {
                            libraries.Add(new LibraryDto(
                                lf.ItemId.ToString(),
                                lf.Name ?? "Unknown",
                                lf.CollectionType?.ToString() ?? "book"));
                        }
                    }
                }
                catch (Exception fetchEx)
                {
                    _logger.LogWarning(fetchEx, "Failed in GetVirtualFolders");
                }
            });

            return Ok(new GetLibrariesResponse(true, libraries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLibraries failed");
            return Ok(new GetLibrariesResponse(false, null, ex.Message));
        }
    }

    /// <summary>
    /// Gets all Jellyfin library IDs of type "book".
    /// Used by sync tasks to get available library IDs.
    /// </summary>
    [HttpGet("LibraryIds")]
    [Authorize(Policy = "DefaultAuthorization")]
    public ActionResult<GetLibraryIdsResponse> GetLibraryIds()
    {
        try
        {
            var ids = _libraryManager.GetVirtualFolders()
                .Where(lf => lf.CollectionType.HasValue && lf.CollectionType.Value.ToString().Contains("book", StringComparison.OrdinalIgnoreCase))
                .Select(lf => lf.ItemId.ToString())
                .ToList();

            return Ok(new GetLibraryIdsResponse(ids));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get library IDs");
            return Ok(new GetLibraryIdsResponse(new List<string>()));
        }
    }
}

/// <summary>Result returned by <c>GET /Komga/TestConnection</c>.</summary>
public record TestConnectionResult(bool Success, string? Error = null);

/// <summary>Result returned by <c>GET /Komga/Config</c>.</summary>
public record KomgaConfigResult(string ExternalUrl);

/// <summary>Response for <c>GET /Komga/Libraries</c>.</summary>
public record GetLibrariesResponse(bool Success, List<LibraryDto>? Libraries = null, string? Error = null);

/// <summary>DTO for a Jellyfin library.</summary>
public record LibraryDto(string Id, string Name, string MediaType);

/// <summary>Response for <c>GET /Komga/LibraryIds</c>.</summary>
public record GetLibraryIdsResponse(List<string> LibraryIds);
