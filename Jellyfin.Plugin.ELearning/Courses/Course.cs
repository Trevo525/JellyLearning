using System.Collections.Generic;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A parsed course folder.
/// </summary>
/// <param name="FolderName">Name of the course folder.</param>
/// <param name="Title">Cleaned-up course title.</param>
/// <param name="Studio">Provider (Udemy, Skillshare, ...), if recognised.</param>
/// <param name="Year">Year from a scene-style folder name, if any.</param>
/// <param name="ArtworkPath">Cover image relative to the course folder, if any.</param>
/// <param name="Sections">Sections in course order.</param>
/// <param name="Resources">Course-level files.</param>
/// <param name="Description">Description from <c>course.json</c>, if any.</param>
public sealed record Course(
    string FolderName,
    string Title,
    string? Studio,
    int? Year,
    string? ArtworkPath,
    IReadOnlyList<CourseSection> Sections,
    IReadOnlyList<CourseFile> Resources,
    string? Description = null);
