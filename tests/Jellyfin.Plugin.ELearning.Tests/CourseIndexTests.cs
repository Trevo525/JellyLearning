using Jellyfin.Plugin.ELearning.Courses;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ELearning.Tests;

public sealed class CourseIndexTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("elearning-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Scans_course_folder_and_resolves_files_by_id_after_reload()
    {
        var library = Directory.CreateDirectory(Path.Combine(_dir, "library")).FullName;
        var course = Path.Combine(library, "Dometrain - Getting Started - Onabaru in .MOM");
        Touch(course, "01. Course anorebag/01. Welcome.mp4", "02. Onabaru/04. What is onabaru.mp4", "code.zip");
        Touch(library, ".hidden-course/1 a.mp4");

        var file = Path.Combine(_dir, "data", "course-index.json");
        new CourseIndex(file, NullLogger<CourseIndex>.Instance)
            .Replace(CourseScanner.ListCourses(library).Select(CourseScanner.Scan));

        var reloaded = new CourseIndex(file, NullLogger<CourseIndex>.Instance);
        var indexed = Assert.Single(reloaded.Courses);
        Assert.Equal("Dometrain", indexed.Course.Studio);
        Assert.Same(indexed, reloaded.GetCourse(course + Path.DirectorySeparatorChar));

        var lecture = indexed.Course.Sections[1].Lectures[0];
        Assert.Equal(Path.Combine(course, "02. Onabaru", "04. What is onabaru.mp4"), reloaded.GetFile(lecture.Id)!.Value.AbsolutePath);
        Assert.Equal(Path.Combine(course, "code.zip"), reloaded.GetFile(indexed.Course.Resources[0].Id)!.Value.AbsolutePath);
    }

    [Fact]
    public void Unknown_ids_and_paths_resolve_to_nothing()
    {
        var index = new CourseIndex(Path.Combine(_dir, "none.json"), NullLogger<CourseIndex>.Instance);
        Assert.Empty(index.Courses);
        Assert.Null(index.GetFile("../../etc/passwd"));
        Assert.Null(index.GetCourse("/nowhere"));
    }

    [Fact]
    public void Corrupt_index_file_is_ignored()
    {
        var file = Path.Combine(_dir, "bad.json");
        File.WriteAllText(file, "{ not json");
        Assert.Empty(new CourseIndex(file, NullLogger<CourseIndex>.Instance).Courses);
    }

    private static void Touch(string root, params string[] relativePaths)
    {
        foreach (var relative in relativePaths)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, []);
        }
    }
}
