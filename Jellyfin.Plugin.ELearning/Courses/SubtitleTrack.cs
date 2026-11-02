namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A sidecar subtitle belonging to a video lecture.
/// </summary>
/// <param name="RelativePath">Path relative to the course folder.</param>
/// <param name="Language">Language tag taken from the file name (e.g. "en"), if any.</param>
/// <param name="Format">Subtitle format, the lower-case extension without dot (srt, vtt, ...).</param>
public sealed record SubtitleTrack(string RelativePath, string? Language, string Format);
