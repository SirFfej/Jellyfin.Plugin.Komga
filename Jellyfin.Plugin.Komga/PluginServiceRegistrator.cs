using Jellyfin.Plugin.Komga.Api;
using Jellyfin.Plugin.Komga.Logging;
using Jellyfin.Plugin.Komga.Providers;
using Jellyfin.Plugin.Komga.Tasks;
using Jellyfin.Plugin.Komga.WebTransformation;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Komga;

/// <summary>
/// Registers the plugin's services with the Jellyfin dependency-injection container.
/// </summary>
/// <remarks>
/// Jellyfin requires this class to have a <b>public parameterless constructor</b> — it is
/// instantiated via reflection before the DI container is built.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // File logger — all ILogger<T> calls from within Jellyfin.Plugin.Komga are
        // automatically mirrored to komga-yyyyMMdd.log in Jellyfin's log directory.
        serviceCollection.AddSingleton<ILoggerProvider>(sp =>
        {
            var paths = sp.GetRequiredService<IApplicationPaths>();
            return new KomgaFileLoggerProvider(paths.LogDirectoryPath);
        });

        serviceCollection.AddHttpClient(KomgaApiClient.HttpClientName, client =>
        {
            client.Timeout = System.TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        serviceCollection.AddSingleton<KomgaApiClientFactory>();

        serviceCollection.AddScoped<IMetadataProvider, KomgaMetadataProvider>();
        serviceCollection.AddScoped<IImageProvider, KomgaImageProvider>();

        serviceCollection.AddScoped<IScheduledTask, SyncKomgaMetadataTask>();
        serviceCollection.AddScoped<IScheduledTask, SyncReadingProgressTask>();

        serviceCollection.AddHostedService<FileTransformationRegistrar>();
    }
}
