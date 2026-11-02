namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A downloadable or viewable file of a course (attachment or resource).
/// </summary>
/// <param name="Id">Stable ID used by the API; files are never addressed by path.</param>
/// <param name="RelativePath">Path relative to the course folder, with '/' separators.</param>
/// <param name="DisplayName">Cleaned-up file name to show.</param>
/// <param name="Kind">The file kind.</param>
public sealed record CourseFile(string Id, string RelativePath, string DisplayName, FileKind Kind);
