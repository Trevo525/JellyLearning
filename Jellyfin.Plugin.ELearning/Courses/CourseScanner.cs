using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// Reads course folders from disk and hands their listings to <see cref="CourseParser"/>.
/// </summary>
public static class CourseScanner
{
    private static readonly EnumerationOptions _recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    /// <summary>
    /// Parses one course folder.
    /// </summary>
    /// <param name="coursePath">Absolute path of the course folder.</param>
    /// <returns>The indexed course.</returns>
    public static IndexedCourse Scan(string coursePath) => Scan(coursePath, null);

    /// <summary>
    /// Parses one course folder.
    /// </summary>
    /// <param name="coursePath">Absolute path of the course folder.</param>
    /// <param name="logger">Where to warn about an unreadable <c>course.json</c>.</param>
    /// <returns>The indexed course.</returns>
    public static IndexedCourse Scan(string coursePath, ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(coursePath);
        var files = Directory.EnumerateFiles(coursePath, "*", _recursive)
            .Select(f => Path.GetRelativePath(coursePath, f))
            .Where(f => f != CourseOverrides.FileName);
        return new IndexedCourse(coursePath, CourseParser.Parse(Path.GetFileName(coursePath), files, Path.TrimEndingDirectorySeparator(coursePath), ReadOverrides(coursePath, logger)));
    }

    /// <summary>
    /// Lists the course folders of a library location (its direct subfolders).
    /// </summary>
    /// <param name="libraryLocation">Absolute path of a library folder.</param>
    /// <returns>Course folder paths.</returns>
    public static IEnumerable<string> ListCourses(string libraryLocation)
        => Directory.Exists(libraryLocation)
            ? Directory.EnumerateDirectories(libraryLocation, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System })
            : [];

    // A course.json that is missing or broken leaves the course as parsed from its file names.
    private static CourseOverrides? ReadOverrides(string coursePath, ILogger? logger)
    {
        var file = Path.Combine(coursePath, CourseOverrides.FileName);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var overrides = CourseOverrides.TryParse(File.ReadAllText(file));
            if (overrides is null)
            {
                logger?.LogWarning("Ignoring {File}: it is not valid JSON of the expected shape", file);
            }

            return overrides;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not read {File}", file);
            return null;
        }
    }
}
