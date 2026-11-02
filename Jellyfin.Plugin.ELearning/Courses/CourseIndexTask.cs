using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// Rebuilds the <see cref="CourseIndex"/> from the configured course libraries after every library scan.
/// </summary>
public sealed class CourseIndexTask : ILibraryPostScanTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly CourseIndex _index;
    private readonly ILogger<CourseIndexTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseIndexTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="index">The course index.</param>
    /// <param name="logger">The logger.</param>
    public CourseIndexTask(ILibraryManager libraryManager, CourseIndex index, ILogger<CourseIndexTask> logger)
    {
        _libraryManager = libraryManager;
        _index = index;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var libraryIds = Plugin.Instance?.Configuration.CourseLibraryIds ?? [];
        var coursePaths = libraryIds
            .Select(id => _libraryManager.GetItemById(id))
            .OfType<CollectionFolder>()
            .SelectMany(library => library.PhysicalLocations)
            .SelectMany(CourseScanner.ListCourses)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var courses = new List<IndexedCourse>(coursePaths.Count);
        for (var i = 0; i < coursePaths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                courses.Add(CourseScanner.Scan(coursePaths[i]));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Skipping unreadable course folder {Path}", coursePaths[i]);
            }

            progress.Report(100.0 * (i + 1) / coursePaths.Count);
        }

        _index.Replace(courses);
        _logger.LogInformation(
            "Indexed {Courses} courses, {Lectures} lectures from {Libraries} course libraries",
            courses.Count,
            courses.Sum(c => c.Course.Sections.Sum(s => s.Lectures.Count)),
            libraryIds.Length);
        return Task.CompletedTask;
    }
}
