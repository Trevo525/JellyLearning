using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Web;

/// <summary>
/// Registers an index.html transformation with the File Transformation plugin so jellyfin-web
/// loads the course UI script. Done by reflection: File Transformation is a runtime dependency
/// only, and without it the plugin still works (just without the injected UI).
/// </summary>
public sealed class WebInjection : IHostedService
{
    private static readonly Guid _transformationId = new("6c1d3a4e-58b2-4f0e-9a51-3e7c2d9b8f10");

    private readonly ILogger<WebInjection> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebInjection"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public WebInjection(ILogger<WebInjection> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// File Transformation callback: adds the script tag to index.html.
    /// </summary>
    /// <param name="payload">The file contents.</param>
    /// <returns>The transformed file.</returns>
    public static string TransformIndex(IndexTransformPayload payload)
        => WebBundle.Inject(payload?.Contents ?? string.Empty);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var register = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.GetName().Name == "Jellyfin.Plugin.FileTransformation")
                ?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")
                ?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            if (register is null)
            {
                _logger.LogWarning("File Transformation plugin not found: the e-learning course UI is not added to the web client");
                return Task.CompletedTask;
            }

            // Build the payload with File Transformation's own JObject type (from its load context).
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                id = _transformationId,
                fileNamePattern = "index.html$",
                callbackAssembly = typeof(WebInjection).Assembly.FullName,
                callbackClass = typeof(WebInjection).FullName,
                callbackMethod = nameof(TransformIndex),
            });
            var jobject = register.GetParameters()[0].ParameterType;
            var payload = jobject.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)])!.Invoke(null, [json]);
            register.Invoke(null, [payload]);
            _logger.LogInformation("Registered the e-learning course UI with File Transformation (bundle {Version})", WebBundle.Version);
        }
        catch (Exception ex) when (ex is TargetInvocationException or InvalidOperationException or MissingMethodException or NullReferenceException)
        {
            _logger.LogError(ex, "Could not register the e-learning course UI with File Transformation");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
