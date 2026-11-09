using System;
using System.Collections.Concurrent;
using System.IO;
using Jellyfin.Plugin.ELearning.Courses;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Parses courses on demand for the resolver and providers (which run before the post-scan
/// <see cref="CourseIndex"/> rebuild), caching each course briefly so one scan parses it once.
/// </summary>
public sealed class CourseCatalog
{
    private static readonly TimeSpan _ttl = TimeSpan.FromSeconds(30);

    private readonly CourseLibraries _libraries;
    private readonly ILogger<CourseCatalog> _logger;
    private readonly ConcurrentDictionary<string, (DateTime ParsedAt, CourseLookup? Lookup)> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCatalog"/> class.
    /// </summary>
    /// <param name="libraries">The course libraries.</param>
    /// <param name="logger">The logger.</param>
    public CourseCatalog(CourseLibraries libraries, ILogger<CourseCatalog> logger)
    {
        _libraries = libraries;
        _logger = logger;
    }

    /// <summary>
    /// Gets the course containing <paramref name="path"/>, if it lies in a course library.
    /// </summary>
    /// <param name="path">Any absolute path.</param>
    /// <returns>The course lookup or null.</returns>
    public CourseLookup? GetCourseFor(string? path)
    {
        var coursePath = _libraries.GetCoursePath(path);
        if (coursePath is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(coursePath, out var cached) && now - cached.ParsedAt < _ttl)
        {
            return cached.Lookup;
        }

        CourseLookup? lookup = null;
        try
        {
            if (Directory.Exists(coursePath))
            {
                lookup = new CourseLookup(CourseScanner.Scan(coursePath, _logger));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read course folder {Path}", coursePath);
        }

        _cache[coursePath] = (now, lookup);
        return lookup;
    }
}
