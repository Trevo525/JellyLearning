namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A course in the index together with the folder it was parsed from.
/// </summary>
/// <param name="Path">Absolute path of the course folder (the Jellyfin Series path).</param>
/// <param name="Course">The parsed course.</param>
public sealed record IndexedCourse(string Path, Course Course);
