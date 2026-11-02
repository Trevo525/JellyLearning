using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// A parsed course with fast lookups by relative path, as needed by the resolver and providers.
/// </summary>
public sealed class CourseLookup
{
    private readonly FrozenDictionary<string, CourseLecture> _lectures;
    private readonly FrozenDictionary<string, CourseSection> _sections;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseLookup"/> class.
    /// </summary>
    /// <param name="indexed">The parsed course.</param>
    public CourseLookup(IndexedCourse indexed)
    {
        ArgumentNullException.ThrowIfNull(indexed);
        Indexed = indexed;
        _lectures = indexed.Course.Sections.SelectMany(s => s.Lectures).ToFrozenDictionary(l => l.RelativePath, StringComparer.Ordinal);
        _sections = indexed.Course.Sections.ToFrozenDictionary(s => s.RelativePath, StringComparer.Ordinal);
    }

    /// <summary>Gets the parsed course and its folder.</summary>
    public IndexedCourse Indexed { get; }

    /// <summary>Gets the course.</summary>
    public Course Course => Indexed.Course;

    /// <summary>
    /// Gets the path of <paramref name="absolutePath"/> relative to the course folder, with '/' separators.
    /// </summary>
    /// <param name="absolutePath">A path inside the course.</param>
    /// <returns>The relative path; empty for the course folder itself.</returns>
    public string Relative(string absolutePath)
    {
        var relative = Path.GetRelativePath(Indexed.Path, absolutePath).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

    /// <summary>Gets the lecture whose main file is at <paramref name="absolutePath"/>.</summary>
    /// <param name="absolutePath">Absolute file path.</param>
    /// <returns>The lecture or null.</returns>
    public CourseLecture? GetLecture(string absolutePath) => _lectures.TryGetValue(Relative(absolutePath), out var lecture) ? lecture : null;

    /// <summary>Gets the section of a folder (empty relative path = implicit section).</summary>
    /// <param name="relativePath">Folder relative to the course.</param>
    /// <returns>The section or null.</returns>
    public CourseSection? GetSection(string relativePath) => _sections.TryGetValue(relativePath, out var section) ? section : null;

    /// <summary>Gets the section that is Jellyfin season <paramref name="seasonNumber"/>.</summary>
    /// <param name="seasonNumber">The season number.</param>
    /// <returns>The section or null.</returns>
    public CourseSection? GetSeason(int seasonNumber) => Course.Sections.FirstOrDefault(s => s.SeasonNumber == seasonNumber);
}
