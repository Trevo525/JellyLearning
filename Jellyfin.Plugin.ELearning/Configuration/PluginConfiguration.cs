using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ELearning.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the IDs of the Shows libraries that are treated as course libraries.
    /// </summary>
#pragma warning disable CA1819 // Arrays are what the XML config serializer round-trips.
    public Guid[] CourseLibraryIds { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets how close to the end (in seconds) a lecture counts as completed.
    /// </summary>
    public int CompletionSecondsFromEnd { get; set; } = 10;

    /// <summary>
    /// Gets or sets the watched percentage at which a lecture counts as completed.
    /// </summary>
    public int CompletionPercent { get; set; } = 98;
}
