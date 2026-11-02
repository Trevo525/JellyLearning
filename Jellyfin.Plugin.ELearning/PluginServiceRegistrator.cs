using Jellyfin.Plugin.ELearning.Courses;
using Jellyfin.Plugin.ELearning.Library;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ELearning;

/// <summary>
/// Registers the plugin's services with the server.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CourseIndex>();
        serviceCollection.AddSingleton<CourseLibraries>();
        serviceCollection.AddSingleton<CourseCatalog>();
    }
}
