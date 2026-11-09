using System.Collections.Generic;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A section of a course (a Jellyfin season when it contains videos).
/// </summary>
/// <param name="Number">Raw number from the folder name, if any.</param>
/// <param name="Title">Cleaned-up title.</param>
/// <param name="RelativePath">Folder relative to the course folder; empty for the implicit section of loose lectures.</param>
/// <param name="SeasonNumber">Jellyfin season number (position among sections with videos); null for document-only sections.</param>
/// <param name="Lectures">Lectures in course order.</param>
/// <param name="Resources">Section-level files.</param>
/// <param name="Description">Description from <c>course.json</c>, if any.</param>
public sealed record CourseSection(
    int? Number,
    string Title,
    string RelativePath,
    int? SeasonNumber,
    IReadOnlyList<CourseLecture> Lectures,
    IReadOnlyList<CourseFile> Resources,
    string? Description = null);
