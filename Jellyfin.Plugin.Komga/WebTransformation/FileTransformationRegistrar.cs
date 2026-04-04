using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Komga.WebTransformation;

/// <summary>
/// Hosted service that registers the Komga HTML transformer with the
/// File Transformation plugin on server startup (if that plugin is installed).
/// </summary>
/// <remarks>
/// Registration is deferred to <see cref="StartAsync"/> — by then all plugin constructors
/// have run and <c>FileTransformationPlugin.Instance</c> is non-null.
/// </remarks>
public class FileTransformationRegistrar : IHostedService
{
    private static readonly Guid TransformationId = new("C4D5E6F7-B2A1-4D0E-9F8C-7B0F9E5D4C3A");
    private const string FtAssemblyName = "Jellyfin.Plugin.FileTransformation";
    private const string FtPluginInterfaceType = "Jellyfin.Plugin.FileTransformation.PluginInterface";
    private const string FtRegisterMethod = "RegisterTransformation";

    private readonly ILogger<FileTransformationRegistrar> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationRegistrar"/> class.
    /// </summary>
    public FileTransformationRegistrar(ILogger<FileTransformationRegistrar> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Locate the File Transformation assembly in the current AppDomain.
            Assembly? ftAssembly = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == FtAssemblyName)
                {
                    ftAssembly = asm;
                    break;
                }
            }

            if (ftAssembly is null)
            {
                _logger.LogDebug("File Transformation plugin not found — Komga reader redirect will not be active.");
                return Task.CompletedTask;
            }

            var pluginInterface = ftAssembly.GetType(FtPluginInterfaceType);
            var registerMethod = pluginInterface?.GetMethod(FtRegisterMethod, BindingFlags.Public | BindingFlags.Static);

            if (registerMethod is null)
            {
                _logger.LogWarning(
                    "Could not find {Type}.{Method} — File Transformation API may have changed.",
                    FtPluginInterfaceType, FtRegisterMethod);
                return Task.CompletedTask;
            }

            var payload = new JObject
            {
                ["id"] = TransformationId.ToString(),
                ["fileNamePattern"] = "index\\.html$",
                ["callbackAssembly"] = typeof(KomgaWebTransformer).Assembly.FullName,
                ["callbackClass"] = typeof(KomgaWebTransformer).FullName,
                ["callbackMethod"] = nameof(KomgaWebTransformer.Transform)
            };

            registerMethod.Invoke(null, new object[] { payload });

            _logger.LogInformation("Registered Komga HTML transformer with File Transformation plugin.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register with File Transformation plugin — reader redirect disabled.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
