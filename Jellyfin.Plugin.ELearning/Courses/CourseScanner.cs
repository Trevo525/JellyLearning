using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public static IndexedCourse Scan(string coursePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(coursePath);
        var files = Directory.EnumerateFiles(coursePath, "*", _recursive)
            .Select(f => Path.GetRelativePath(coursePath, f));
        return new IndexedCourse(coursePath, CourseParser.Parse(Path.GetFileName(coursePath), files, Path.TrimEndingDirectorySeparator(coursePath)));
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
}
