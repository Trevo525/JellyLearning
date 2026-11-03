namespace Jellyfin.Plugin.ELearning.Courses;

/// <summary>
/// A file of an indexed course, resolved from its ID.
/// </summary>
/// <param name="Course">The course the file belongs to.</param>
/// <param name="File">The file (for a lecture: its main file).</param>
/// <param name="Lecture">The lecture whose main file this is, or null for attachments and resources.</param>
/// <param name="AbsolutePath">Absolute path of the file; always inside the course folder.</param>
public sealed record IndexedFile(IndexedCourse Course, CourseFile File, CourseLecture? Lecture, string AbsolutePath);
