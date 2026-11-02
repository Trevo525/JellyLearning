using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Knows which library folders are course libraries (ticked in the plugin settings), so every
/// resolver and provider of this plugin leaves all other libraries alone.
/// </summary>
public sealed class CourseLibraries
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseLibraries"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    public CourseLibraries(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the folders of all course libraries.
    /// </summary>
    /// <returns>Absolute folder paths without trailing separator.</returns>
    public IReadOnlyList<string> GetLocations()
        => (Plugin.Instance?.Configuration.CourseLibraryIds ?? [])
            .Select(id => _libraryManager.GetItemById(id))
            .OfType<CollectionFolder>()
            .SelectMany(library => library.PhysicalLocations)
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Gets the course folder that contains <paramref name="path"/> (or is it).
    /// </summary>
    /// <param name="path">Any absolute path.</param>
    /// <returns>The course folder, or null when the path is not inside a course library course.</returns>
    public string? GetCoursePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var location in GetLocations())
        {
            if (path.Length > location.Length + 1
                && path.StartsWith(location, StringComparison.Ordinal)
                && path[location.Length] == Path.DirectorySeparatorChar)
            {
                var rest = path.AsSpan(location.Length + 1);
                var slash = rest.IndexOf(Path.DirectorySeparatorChar);
                return slash < 0 ? path : path[..(location.Length + 1 + slash)];
            }
        }

        return null;
    }
}
