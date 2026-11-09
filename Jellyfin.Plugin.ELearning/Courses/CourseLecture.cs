using System.Collections.Generic;
using System.IO;

namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// One lecture of a section: a video, or a document shown in the outline (HTML, quiz, PDF, ...).
/// </summary>
/// <param name="Id">Stable ID of the lecture's main file.</param>
/// <param name="Kind">The kind of the lecture's main file.</param>
/// <param name="Number">Raw number from the file name ("Lecture 04"), if any.</param>
/// <param name="Title">Cleaned-up title.</param>
/// <param name="RelativePath">Path of the main file relative to the course folder.</param>
/// <param name="EpisodeNumber">Jellyfin episode number (position among the section's videos); null for documents.</param>
/// <param name="Attachments">Files that belong to this lecture.</param>
/// <param name="Subtitles">Sidecar subtitles (videos only).</param>
/// <param name="Description">Description from <c>course.json</c>, if any.</param>
public sealed record CourseLecture(
    string Id,
    FileKind Kind,
    int? Number,
    string Title,
    string RelativePath,
    int? EpisodeNumber,
    IReadOnlyList<CourseFile> Attachments,
    IReadOnlyList<SubtitleTrack> Subtitles,
    string? Description = null)
{
    /// <summary>
    /// Gets the lecture's main file as a <see cref="CourseFile"/>.
    /// </summary>
    /// <returns>The main file.</returns>
    public CourseFile ToFile() => new(Id, RelativePath, CourseNaming.DisplayName(Path.GetFileName(RelativePath)), Kind);
}
